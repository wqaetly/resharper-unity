package model.backendUnity

import com.jetbrains.rd.generator.nova.*
import com.jetbrains.rd.generator.nova.PredefinedType.*
import com.jetbrains.rd.generator.nova.csharp.CSharp50Generator
import model.lib.Library
import model.lib.Library.ProfilingData

// backend <-> Unity Editor model, from point of view of backend, meaning:
// Sink is a one-way signal the backend subscribes to (editor fires)
// Source is a one-way signal the backend fires (editor subscribes)
// Signal is a two-way signal that either end can subscribe to and fire
// Property and Signal are two-way and can be updated/fired on both ends. Property is stateful.
// Call is an RPC method (with return value) that is called by the backend/implemented by the Unity Editor
// Callback is an RPC method (with return value) that is implemented by the backend/called by the Unity Editor
@Suppress("unused")
object BackendUnityModel: Root() {

    private var RdOpenFileArgs = structdef {
        field("path", string)
        field("line", int)
        field("col", int)
    }

    private val FindUsagesSessionResult = structdef {
        field("target", string)
        field("elements", array(AssetFindUsagesResultBase))
    }

    private val AssetFindUsagesResultBase = basestruct {
        field("expandInTreeView", bool)
        field("filePath", string)
        field("fileName", string)
        field("extension", string)
    }

    private val AssetFindUsagesResult = structdef extends AssetFindUsagesResultBase {
    }

    private val HierarchyFindUsagesResult = structdef extends AssetFindUsagesResultBase {
        field("pathElements", array(string))
        field("rootIndices", array(int))
    }

    private val AnimatorFindUsagesResult = structdef extends AssetFindUsagesResultBase {
        field("pathElements", array(string))
        field("type", enum("AnimatorUsageType") {
            +"State"
            +"StateMachine"
        })
    }

    private val AnimationFindUsagesResult = structdef extends AssetFindUsagesResultBase {}

    private val TestResult = structdef {
        field("testId", string)
        field("projectName", string)
        field("output", string)
        field("duration", int)
        field("status", enum {
            +"Pending"
            +"Running"
            +"Inconclusive"
            +"Ignored"
            +"Success"
            +"Failure"
        })
        field("parentId", string)
    }

    private val TestFilter = structdef {
        field("assemblyName", string)
        field("testNames", immutableList(string))
        field("groupNames", immutableList(string))
        field("testCategories", immutableList(string))
    }

    private val UnitTestLaunchClientControllerInfo = structdef {
        field("codeBase", string)
        field("codeBaseDependencies", immutableList(string).nullable)
        field("typeName", string)
    }

    private val UnitTestLaunch = classdef {
        field("sessionId", guid)
        field("testFilters", immutableList(TestFilter))
        field("testMode", enum("TestMode") {
            +"Both"
            +"Edit"
            +"Play"
        })
        field("clientControllerInfo", UnitTestLaunchClientControllerInfo.nullable)
        property("runStarted", bool)
        sink("testResult", TestResult)
        sink("runResult", structdef("RunResult") {
            field("passed", bool)
        })
        call("abort", void, bool)
    }

    private val RefreshType = enum {
        +"ForceRequestScriptReload"
        +"Force"
        +"Normal"
    }
    val UnityPlatformInfo = structdef {
        field("activeBuildTarget", string)
        field("installedModules", immutableList(string))
        field("buildTargetGroupsWithIcons", immutableList(string))
    }

    init {
        setting(CSharp50Generator.Namespace, "JetBrains.Rider.Model.Unity.BackendUnity")

        // *************************************************************************************************************
        //
        // WARNING!
        //
        // Be very careful about stateful data, e.g. RD properties or locally cached values
        //
        // Properties are stateful, and are not reset to a default value when a connection is closed. This means the
        // model in the backend will contain stale values when the Unity Editor connection is lost. This will happen
        // every time the Unity Editor reloads an AppDomain (e.g. enters play mode, script compilation, etc) and of
        // course when the Unity Editor is closed.
        //
        // Properties MUST be set to an initial value every time the Unity Editor model is created (on initial editor
        // launch, and on every AppDomain reload).
        //
        // Take care with stale properties in the backend when the Unity Editor connection is lost. These properties
        // will contain stale values, and SHOULD be rest if this stale data is dangerous (e.g. process IDs would be
        // invalid, whereas application paths can be incorrect, but less likely to cause serious issues).
        //
        // Also take care with locally cached values. Make sure to update correctly when the protocol is reset, either
        // using them to set initial values in the protocol, or updated from the protocol.
        //
        // *************************************************************************************************************

        // TODO: Is this useful? Can we just try to call openFileLineCol directly?
        callback("isBackendConnected", void, bool).documentation = "Called from Unity to ensure backend is connected before opening a file"

        // TODO: This should be a simple property, reset when the protocol is lost
        call("getUnityEditorState", void, Library.UnityEditorState).documentation = "Polled from the backend to get what the editor is currently doing"

        property("unityApplicationData", Library.UnityApplicationData).async
        property ("riderPackagePotentialUpdateVersion", string).documentation = "Version of the latest compatible update for the Rider package"
        field("unityApplicationSettings", Library.UnityApplicationSettings)
        field("unityProjectSettings", Library.UnityProjectSettings)
        property("unityPlatformInfo", UnityPlatformInfo).async

        // Rider application settings (frontend)
        property("riderProcessId", int).documentation = "The process ID of the frontend, set by the backend. Unity uses this in a call to AllowSetForegroundWindow, so that Rider can bring itself to the foreground when opening a file"

        field("playControls", Library.PlayControls)
        field("consoleLogging", Library.ConsoleLogging)

        // Actions called from the backend to Unity
        // (These should probably be calls rather than signals, as they are definitely RPC calls, not events)
        signal("showPreferences", void).documentation = "Opens the preferences dialog in Unity"
        signal("showFileInUnity", string).documentation = "Switches to Unity, focuses the Project view and selects and pings the requested file"
        signal("showUsagesInUnity", AssetFindUsagesResultBase).documentation = "Switches to Unity, focuses the Project view, select and ping either the selected file (prefab) or Inspector object"
        signal("sendFindUsagesSessionResult", FindUsagesSessionResult).documentation = "Sends Find Usages results to Unity, to display in a tool window"

        call("updateUnityPlugin", string, bool)
        call("exitUnity", void, bool)
        call("refresh", RefreshType, void).documentation = "Refresh the asset database"
        call("getCompilationResult", void, bool).documentation = "Called after Refresh to get the compilation result before launching unit tests"
        call("generateUIElementsSchema", void, bool).documentation = "Generates the UIElements schema, if available"
        call("runMethodInUnity", Library.RunMethodData, Library.RunMethodResult)
        call("getAndroidSdkRoot", void, string.nullable).documentation = "Get the currently configured Android SDK root location, if available"

        // Actions called from Unity to the backend
        callback("openFileLineCol", RdOpenFileArgs, bool).documentation = "Called from Unity to quickly open a file in an existing Rider instance"

        // Unit testing
        property("unitTestLaunch", UnitTestLaunch).documentation = "Set the details of the current unit test session"
        call("runUnitTestLaunch", void, bool).documentation = "Start the unit test session. Results are fired via UnitTestLaunch.TestResult"

        call ("hasUnsavedState", void, bool)

        // profiler
        call ("startProfiling", ProfilingData, void).documentation = "Start profiling and enter PlayMode, depending on the param"
        call ("stopProfiling", ProfilingData, void)
    }
}

object UnityProfilerModel : Ext(BackendUnityModel) {

    private val SampleStackInfo = structdef {
        field("sampleName", string.nullable)
        field("sampleStack", string.nullable)
    }
    private val SampleInfo = structdef {
        field("duration", double)
        field("markerId", int)
        field("childrenCount", int)
    }
    private val MarkerToNamePair = structdef {
        field("markerId", int)
        field("name", string)
    }
    private val UnityProfilerSnapshot = structdef {
        field("frameIndex", int)
        field("startTimeMs", double)
        field("frameTimeMs", float)
        field("threadIndex", int)
        field("threadName", string)
        field("markerIdToName", immutableList(MarkerToNamePair))
        field("samples", immutableList(SampleInfo))
        field("status", Library.UnityProfilerSnapshotStatus)
    }

    init {
        //Unity profiler integration
        callback("openFileBySampleInfo", SampleStackInfo, void).documentation = "Called from Unity to navigate from selected profiler sample"
        call("getUnityProfilerSnapshot", Library.ProfilerSnapshotRequest, UnityProfilerSnapshot.nullable).async.documentation = "Polled from the backend to get current frame profiler snapshot data"
        property("profilerSnapshotStatus", Library.UnityProfilerSnapshotStatus).async
    }
}
