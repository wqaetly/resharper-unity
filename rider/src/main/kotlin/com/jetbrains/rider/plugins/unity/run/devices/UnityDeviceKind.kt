package com.jetbrains.rider.plugins.unity.run.devices

import com.intellij.openapi.actionSystem.AnAction
import com.jetbrains.rider.plugins.unity.UnityBundle
import com.jetbrains.rider.run.devices.DeviceKind
import org.jetbrains.annotations.Nls

open class UnityDeviceKind(@Nls name: String) : DeviceKind(name, name) {
  override fun getMissingDevicesAction(): AnAction? {
    return null
  }
}

object UnityUsbDeviceKind : UnityDeviceKind(UnityBundle.message("project.name.usb.devices")) {
}

object UnityCustomPlayerDeviceKind : UnityDeviceKind(UnityBundle.message("unity.custom.devices.kind.name")) {
}

object UnityRemotePlayerDeviceKind : UnityDeviceKind(UnityBundle.message("unity.remote.devices.kind.name")) {
}

object UnityLocalPlayerDeviceKind : UnityDeviceKind(UnityBundle.message("unity.local.devices.kind.name")) {
}

object UnityLocalUwpPlayerDeviceKind : UnityDeviceKind(UnityBundle.message("unity.local.uwp.devices.kind.name")) {
}

object UnityEditorDeviceKind : UnityDeviceKind(UnityBundle.message("unity.editor.devices.kind.name")) {
}

object UnityVirtualPlayerDeviceKind : UnityDeviceKind(UnityBundle.message("unity.virtual.player.devices.kind.name")) {
}