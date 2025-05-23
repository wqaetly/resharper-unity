package model.lib

import com.jetbrains.rd.generator.nova.*
import com.jetbrains.rd.generator.nova.PredefinedType.*
import com.jetbrains.rd.generator.nova.csharp.CSharp50Generator
import com.jetbrains.rd.generator.nova.kotlin.Kotlin11Generator

object Library : Root() {

    init {
        setting(Kotlin11Generator.Namespace, "com.jetbrains.rider.plugins.unity.model")
        setting(CSharp50Generator.Namespace, "JetBrains.Rider.Model.Unity")
    }

    override val isLibrary = true

    val UnityEditorState = enum {
        +"Disconnected"
        +"Idle"
        +"Play"
        +"Pause"
        +"Refresh"
    }

    // This is a structdef because the values do not change during the lifetime of the application
    // (Note that the struct might be set and populate via heuristics, not necessarily a running instance of Unity)
    val UnityApplicationData = structdef {
        field("applicationPath", string)
        field("applicationContentsPath", string)
        field("applicationVersion", string)
        field("editorLogPath", string.nullable).documentation = "Editor log path. Will be null when Unity protocol is not connected"
        field("playerLogPath", string.nullable).documentation = "Player log path. Will be null when Unity protocol is not connected"
        field("unityProcessId", int.nullable).documentation = "Used by the test runner and the frontend uses it in a call " +
            "to AllowSetForegroundWindow to allow Unity to bring itself to the foreground, e.g. when opening an .asmdef file." +
            "Will be null when the Unity protocol is not connected"
    }

    val UnityApplicationSettings = aggregatedef("UnityApplicationSettings") {
        property("scriptCompilationDuringPlay", enum("ScriptCompilationDuringPlay") {
            +"RecompileAndContinuePlaying"
            +"RecompileAfterFinishedPlaying"
            +"StopPlayingAndRecompile"
        })
        signal("profilerSnapshotFetchingSettings", int).async.documentation = "What's new action to enable Unity Profiler integration feature"
    }

    val UnityProjectSettings = aggregatedef("UnityProjectSettings") {
        property("buildLocation", string).documentation = "Path to the executable of the last built Standalone player, if it exists. Can be empty"
    }

    val RunMethodData = structdef {
        field("assemblyName", string)
        field("typeName", string)
        field("methodName", string)
    }

    val RunMethodResult = structdef {
        field("success", bool)
        field("message", string)
        field("stackTrace", string)
    }

    val PlayControls = aggregatedef("PlayControls") {
        property("play", bool)
        property("pause", bool)
        signal("step", void)
    }

    private val LogEvent = structdef {
        field("time", long)
        field("type", enum("LogEventType") {
            +"Error"
            +"Warning"
            +"Message"
        })
        field("mode", enum("LogEventMode") {
            +"Edit"
            +"Play"
        })
        field("message", string)
        field("stackTrace", string)
    }

    val ConsoleLogging = aggregatedef("ConsoleLogging") {
        sink("onConsoleLogEvent", LogEvent)
        property("lastPlayTime", long)
        property("lastInitTime", long)
    }

    val ProfilingData = structdef {
        field("enterPlayMode", bool)
        field("unityProfilerApiPath", string)
        field("needRestartScripts", bool)
    }

    val UnityProfilerSnapshotStatus = structdef {
        field("frameIndex", int)
        field("threadIndex", int)
        field("threadName", string)
        field("samplesCount", int)
        field("status", enum("SnapshotStatus") {
            +"Disabled"
            +"NoSnapshotDataAvailable"
            +"HasNewSnapshotDataToFetch"
            +"SnapshotDataFetchingInProgress"
            +"SnapshotDataIsUpToDate"
        })
        field("fetchingProgress", float)
    }
    
    val ProfilerSnapshotRequest = structdef{
        field("frameIndex", int)
        field("threadIndex", int)
    }
}