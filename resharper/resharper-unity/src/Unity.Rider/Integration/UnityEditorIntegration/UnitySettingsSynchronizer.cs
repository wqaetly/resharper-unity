﻿using System;
using System.Linq.Expressions;
using JetBrains.Application.Components;
using JetBrains.Application.Parts;
using JetBrains.Application.Settings;
using JetBrains.Application.Threading;
using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Reflection;
using JetBrains.ReSharper.Plugins.Unity.Core.Application.Settings;
using JetBrains.ReSharper.Plugins.Unity.Rider.Integration.Protocol;
using JetBrains.ReSharper.Psi.Util;
using JetBrains.Rider.Model.Unity.FrontendBackend;

namespace JetBrains.ReSharper.Plugins.Unity.Rider.Integration.UnityEditorIntegration
{
    // can't make it IUnityLazyComponent, when working with a class lib, there is a chance that lib is later used in Unity project 
    // ideally we should init this component, only when backendSettings is asked on the frontend
    [SolutionComponent(Instantiation.DemandAnyThreadSafe)]
    public class UnitySettingsSynchronizer : IStartupActivity
    {
        public UnitySettingsSynchronizer(Lifetime lifetime, ISolution solution, FrontendBackendHost host,
                                         IApplicationWideContextBoundSettingStore settingsStore)
        {
            var boundStore = settingsStore.BoundSettingsStore;
            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.EnableShaderLabHippieCompletion,
                (model, args) => model.BackendSettings.EnableShaderLabHippieCompletion.Value = args.New);

            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.UseUnityYamlMerge,
                (model, args) => model.BackendSettings.UseUnityYamlMerge.Value = args.New);
            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.MergeParameters,
                (model, args) => model.BackendSettings.MergeParameters.Value = args.New);

            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.EnableDebuggerExtensions,
                (model, args) => model.BackendSettings.EnableDebuggerExtensions.Value = args.New);
            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.IgnoreBreakOnUnhandledExceptionsForIl2Cpp,
                (model, args) => model.BackendSettings.IgnoreBreakOnUnhandledExceptionsForIl2Cpp.Value = args.New);
            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.ForcedTimeoutForAdvanceUnityEvaluation,
                (model, args) => model.BackendSettings.ForcedTimeoutForAdvanceUnityEvaluation.Value = args.New);
            BindSettingToProperty(lifetime, solution, host, boundStore,
                (UnitySettings s) => s.BreakpointTraceOutput,
                (model, args) => model.BackendSettings.BreakpointTraceOutput.Value = (int)args.New);
        }

        private static void BindSettingToProperty<TKeyClass, TEntryMemberType>(
            Lifetime lifetime, ISolution solution, FrontendBackendHost frontendBackendHost,
            IContextBoundSettingsStoreLive boundStore,
            Expression<Func<TKeyClass, TEntryMemberType>> entry,
            Action<FrontendBackendModel, PropertyChangedEventArgs<TEntryMemberType>> action)
        {
            var name = entry.GetInstanceMemberName();
            var setting = boundStore.Schema.GetScalarEntry(entry);
            var apartmentForNotifications = ApartmentForNotifications.Primary(solution.Locks);
            boundStore.GetValueProperty2<TEntryMemberType>(lifetime, setting, null, apartmentForNotifications).Change.Advise_HasNew(lifetime,
                args =>
                {
                    solution.Locks.ExecuteOrQueueEx(lifetime, name, () =>
                    {
                        frontendBackendHost.Do(m => action(m, args));
                    });
                });
        }
    }
}