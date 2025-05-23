#nullable enable

using System;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.Application.Threading;
using JetBrains.Collections.Viewable;
using JetBrains.Core;
using JetBrains.Diagnostics;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Rd.Base;
using JetBrains.Rd.Tasks;
using JetBrains.ReSharper.Plugins.Unity.Core.Application.Components;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Rider.Model.Unity;
using JetBrains.Rider.Model.Unity.BackendUnity;
using JetBrains.Rider.Model.Unity.FrontendBackend;
using JetBrains.Util;
using FrontendOpenArgs = JetBrains.Rider.Model.Unity.FrontendBackend.RdOpenFileArgs;

namespace JetBrains.ReSharper.Plugins.Unity.Rider.Integration.Protocol
{
    [SolutionComponent(Instantiation.DemandAnyThreadSafe)]
    public class PassthroughHost : IUnityProjectLazyComponent
    {
        private readonly BackendUnityHost myBackendUnityHost;
        private readonly FrontendBackendHost myFrontendBackendHost;
        private readonly ILogger myLogger;

        public PassthroughHost(Lifetime lifetime,
            BackendUnityHost backendUnityHost,
            FrontendBackendHost frontendBackendHost,
            ILogger logger,
            IThreading threading)
        {
            myBackendUnityHost = backendUnityHost;
            myFrontendBackendHost = frontendBackendHost;
            myLogger = logger;

            if (!frontendBackendHost.IsAvailable) return;
            var model = frontendBackendHost.Model;
            if (model == null) return; // only tests

            threading.ExecuteOrQueueEx(lifetime, "PassthroughHost.ctor", () =>
            {
                AdviseFrontendToUnityModel(lifetime, model);

                // Advise the backend/Unity model as high priority so we get called back before other subscribers.
                // This allows us to populate the protocol on reconnection before other subscribes start to advise
                using (Signal.PriorityAdviseCookie.Create())
                {
                    // TODO: ReactiveEx.ViewNotNull isn't NRT ready
                    backendUnityHost.BackendUnityModel!.ViewNotNull<BackendUnityModel>(lifetime,
                        (l, backendUnityModel) =>
                        {
                            Assertion.AssertNotNull(backendUnityModel);
                            AdviseUnityToFrontendModel(l, backendUnityModel);
                        });
                }

                backendUnityHost.BackendUnityModel.Advise(lifetime, backendUnityModel =>
                {
                    // https://github.com/JetBrains/resharper-unity/pull/2023
                    if (backendUnityModel == null) frontendBackendHost.Model?.PlayControlsInitialized.SetValue(false);
                });
            });
        }

        private void AdviseFrontendToUnityModel(Lifetime lifetime, FrontendBackendModel frontendBackendModel)
        {
            // BackendUnityModel is recreated frequently (e.g. on each AppDomain reload when changing play/edit mode).
            // So subscribe to the frontendBackendModel once and flow in changes only if backendUnityModel is available.
            // Note that we only flow changes, not the current value. Even though these properties are stateful,
            // frontendBackendModel is not the source of truth - values need to flow from backendUnityModel. Also, due
            // to model reload, we go through a few values before we stabilise. E.g.:
            // * User clicks play, fb.Play is true, flows into bu.Play which switches to play mode and causes an
            //   AppDomain reload.
            // * bu.Play becomes false due to AppDomain teardown, flows into fb.Play
            // * BackendUnityModel is torn down and recreated (<- WARNING!)
            // * bu.Play becomes true as Unity enters play mode, flows into fb.Play
            // If we flowed the current value of fb.Play into backendUnityModel when it is recreated, we'd set it to
            // false, triggering play mode to end.
            // Step is simply since it's a non-stateful ISource<T>
            var backendUnityModelProperty = myBackendUnityHost.BackendUnityModel;

            frontendBackendModel.PlayControls.Play.FlowChangesIntoRdDeferred(lifetime,
                () => backendUnityModelProperty.Maybe.ValueOrDefault?.PlayControls.Play);
            frontendBackendModel.PlayControls.Pause.FlowChangesIntoRdDeferred(lifetime,
                () => backendUnityModelProperty.Maybe.ValueOrDefault?.PlayControls.Pause);
            frontendBackendModel.PlayControls.Step.Advise(lifetime, () => backendUnityModelProperty.Maybe.ValueOrDefault?.PlayControls.Step.Fire());


            frontendBackendModel.UnityApplicationSettings.ProfilerSnapshotFetchingSettings.Advise(lifetime,
                settingValue =>
                {
                    backendUnityModelProperty.Maybe.ValueOrDefault?.UnityApplicationSettings
                        .ProfilerSnapshotFetchingSettings.Fire(settingValue);
                });

            // Called from frontend to generate the UIElements schema files
            frontendBackendModel.GenerateUIElementsSchema.SetAsync((l, u) =>
            {
                var model = backendUnityModelProperty.Maybe.ValueOrDefault;
                return model != null
                    ? model.GenerateUIElementsSchema.Start(l, u).ToRdTask(l)
                    : RdTask.Successful(false);
            });

            frontendBackendModel.GetAndroidSdkRoot.SetAsync(async (l, u) =>
            {
                var model = backendUnityModelProperty.Maybe.ValueOrDefault;
                return model == null ? null : await model.GetAndroidSdkRoot.Start(l, u).AsTask();
            });

            // Signalled from frontend to select and ping the object in the Project view
            frontendBackendModel.ShowFileInUnity.Advise(lifetime, file =>
                backendUnityModelProperty.Maybe.ValueOrDefault?.ShowFileInUnity.Fire(file));

            // Signalled from fronted to open the preferences window
            frontendBackendModel.ShowPreferences.Advise(lifetime, _ =>
                backendUnityModelProperty.Maybe.ValueOrDefault?.ShowPreferences.Fire());

            // Called from frontend to run a method in unity
            frontendBackendModel.RunMethodInUnity.SetAsync((l, data) =>
            {
                var backendUnityModel = backendUnityModelProperty.Maybe.ValueOrDefault;
                return backendUnityModel == null
                    ? RdTask.Cancelled<RunMethodResult>()
                    : backendUnityModel.RunMethodInUnity.Start(l, data).ToRdTask(l);
            });

            frontendBackendModel.HasUnsavedState.SetAsync((l, u) =>
            {
                var backendUnityModel = backendUnityModelProperty.Maybe.ValueOrDefault;
                return backendUnityModel == null
                    ? RdTask.Cancelled<bool>()
                    : backendUnityModel.HasUnsavedState.Start(l, u).ToRdTask(l);
            });

            frontendBackendModel.StartProfiling.SetAsync((l, play) =>
            {
                var backendUnityModel = backendUnityModelProperty.Maybe.ValueOrDefault;
                var profilerApiPath = GetProfilerApiPath();
                return backendUnityModel == null || profilerApiPath == null
                    ? RdTask.Cancelled<Unit>()
                    : backendUnityModel.StartProfiling.Start(l, new ProfilingData(play, profilerApiPath, false)).ToRdTask(l);
            });
        }

        private string? GetProfilerApiPath()
        {
            var etwAssemblyShorName = "JetBrains.Etw";
            var etwAssemblyLocation = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Equals(etwAssemblyShorName))?.Location;
            if (etwAssemblyLocation == null)
            {
                myLogger.Error($"{etwAssemblyShorName} was not found.");
                return null;
            }
            var unityProfilerApiPath = FileSystemPath.Parse(etwAssemblyLocation).Parent
                .Combine("JetBrains.Etw.UnityProfilerApi.dll");
            if (!unityProfilerApiPath.ExistsFile)
            {
                myLogger.Error($"{unityProfilerApiPath} doesn't exist.");
                return null;
            }
            return unityProfilerApiPath.FullPath;
        }

        private void AdviseUnityToFrontendModel(Lifetime lifetime, BackendUnityModel backendUnityModel)
        {
            // *********************************************************************************************************
            //
            // WARNING
            //
            // Be very careful with stateful properties!
            //
            // When the backend/Unity protocol is closed, the existing properties maintain their current values. This
            // doesn't affect BackendUnityModel because we clear the model when the connection is lost. However, it does
            // affect any properties that have had values flowed in from BackedUnityModel - these values are not reset.
            //
            // When the backend/Unity protocol is (re)created and advertised, we *should* have initial values from the
            // Unity end (the model is advertised asynchronously to being created, and the dispatcher *should* have
            // processed messages). However, we cannot guarantee this - during testing, it usually works as expected,
            // but occasionally wouldn't be fully initialised. These means we need to be careful when assuming that
            // initial values are available in the properties. Advise and RdExtensions.FlowIntoRdSafe will correctly set
            // the target value if the source value exists. Avoid BeUtilExtensions.FlowIntoRd, as that will throw an
            // exception if the source value does not yet exist.
            // Note that creating and advertising the model, as well as all callbacks, happen on the main thread.
            //
            // We must ensure that the Unity end (re)initialises properties when the protocol is created, or we could
            // have stale or empty properties here and in the frontend.
            //
            // *********************************************************************************************************

            var frontendBackendModel = myFrontendBackendHost.Model.NotNull("frontendBackendModel != null");
            AdviseApplicationData(lifetime, backendUnityModel, frontendBackendModel);
            AdviseApplicationSettings(lifetime, backendUnityModel, frontendBackendModel);
            AdviseProjectSettings(lifetime, backendUnityModel, frontendBackendModel);
            AdvisePlayControls(lifetime, backendUnityModel, frontendBackendModel);
            AdviseConsoleEvents(lifetime, backendUnityModel, frontendBackendModel);
            AdviseOpenFile(backendUnityModel, frontendBackendModel);
        }

        private static void AdviseApplicationData(in Lifetime lifetime, BackendUnityModel backendUnityModel,
                                                  FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.UnityApplicationData.FlowIntoRdSafe(lifetime, frontendBackendModel.UnityApplicationData);
            backendUnityModel.UnityApplicationData.FlowIntoRdSafe(lifetime, data =>
            {
                var version = UnityVersion.Parse(data.ApplicationVersion);
                return UnityVersion.RequiresRiderPackage(version);
            }, frontendBackendModel.RequiresRiderPackage);
            
            backendUnityModel.UnityApplicationData.FlowIntoRdSafe(lifetime, data =>
            {
                var version = UnityVersion.Parse(data.ApplicationVersion);
                return UnityVersion.IsCoreCLR(version);
            }, frontendBackendModel.IsCoreCLR);
        }

        private static void AdviseApplicationSettings(in Lifetime lifetime, BackendUnityModel backendUnityModel,
                                                      FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.UnityApplicationSettings.ScriptCompilationDuringPlay.FlowIntoRdSafe(lifetime,
                frontendBackendModel.UnityApplicationSettings.ScriptCompilationDuringPlay);
        }

        private static void AdviseProjectSettings(in Lifetime lifetime, BackendUnityModel backendUnityModel,
                                                  FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.UnityProjectSettings.BuildLocation.FlowIntoRdSafe(lifetime,
                frontendBackendModel.UnityProjectSettings.BuildLocation);
        }


        private static void AdvisePlayControls(in Lifetime lifetime, BackendUnityModel backendUnityModel,
                                               FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.PlayControls.Play.FlowIntoRdSafe(lifetime, frontendBackendModel.PlayControls.Play);
            backendUnityModel.PlayControls.Pause.FlowIntoRdSafe(lifetime, frontendBackendModel.PlayControls.Pause);
            // https://github.com/JetBrains/resharper-unity/pull/2023
            backendUnityModel.PlayControls.Play.Advise(lifetime, _ => frontendBackendModel.PlayControlsInitialized.SetValue(true));
        }

        private static void AdviseConsoleEvents(in Lifetime lifetime, BackendUnityModel backendUnityModel,
                                                FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.ConsoleLogging.OnConsoleLogEvent.Advise(lifetime, frontendBackendModel.ConsoleLogging.OnConsoleLogEvent.Fire);

            backendUnityModel.ConsoleLogging.LastInitTime.FlowIntoRdSafe(lifetime, frontendBackendModel.ConsoleLogging.LastInitTime);
            backendUnityModel.ConsoleLogging.LastPlayTime.FlowIntoRdSafe(lifetime, frontendBackendModel.ConsoleLogging.LastPlayTime);
        }

        private void AdviseOpenFile(BackendUnityModel backendUnityModel, FrontendBackendModel frontendBackendModel)
        {
            backendUnityModel.OpenFileLineCol.SetAsync((lf, args) =>
            {
                RdTask<bool> result = new RdTask<bool>();
                using (ReadLockCookie.Create())
                {
                    try
                    {
                        return frontendBackendModel.OpenFileLineCol.Start(lf, new FrontendOpenArgs(args.Path, args.Line, args.Col)).ToRdTask(lf);
                    }
                    catch (Exception e)
                    {
                        myLogger.Error(e);
                        result.Set(false);
                    }
                }

                return result;
            });
        }
    }
}
