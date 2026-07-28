#pragma once

// Onyx native shim — registers a Media Foundation Virtual Camera with Windows
// so any app (Zoom, Discord, OBS) sees the processed feed as a normal webcam.
//
// Requires Windows 11 and the Windows SDK (mfvirtualcamera.h).
// This header is intentionally minimal until the C++ toolchain is installed.

#ifdef ONYXNATIVE_EXPORTS
#define ONYX_API __declspec(dllexport)
#else
#define ONYX_API __declspec(dllimport)
#endif

extern "C" {

// Registers and starts the Onyx virtual camera. Returns an HRESULT (0 == S_OK).
ONYX_API long OnyxVirtualCameraStart();

// Stops and removes the virtual camera.
ONYX_API long OnyxVirtualCameraStop();

}  // extern "C"
