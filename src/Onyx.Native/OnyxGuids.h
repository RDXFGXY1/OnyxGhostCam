#pragma once
#include <guiddef.h>

// Stable identity for the Onyx software media source.
//
// This CLSID is what we hand to MFCreateVirtualCamera as the source id, and what
// the Windows Frame Server uses to CoCreateInstance our media source inside its
// own process. It must stay constant across builds once we register it.
//
// {6B2E4F1A-9C3D-4A7E-B8F5-2D1C3E4A5B60}
DEFINE_GUID(CLSID_OnyxMediaSource,
    0x6b2e4f1a, 0x9c3d, 0x4a7e, 0xb8, 0xf5, 0x2d, 0x1c, 0x3e, 0x4a, 0x5b, 0x60);

namespace onyx {

// Default output format of the virtual camera (milestone 1: fixed).
constexpr unsigned kFrameWidth  = 1280;
constexpr unsigned kFrameHeight = 720;
constexpr unsigned kFrameRateNum = 30;
constexpr unsigned kFrameRateDen = 1;

// NV12: Y plane (w*h) + interleaved UV plane (w*h/2) => w*h*3/2 total.
// The Frame Server's MASTER capture pipeline expects this standard camera format.
constexpr unsigned kFrameStride = kFrameWidth;                       // NV12 Y-plane stride
constexpr unsigned kFrameSize   = kFrameWidth * kFrameHeight * 3 / 2; // NV12 total bytes

}  // namespace onyx
