// Onyx virtual-camera host (milestone 1c).
//
// Creates the Onyx virtual camera from our registered media source (by CLSID),
// starts it, and keeps it alive until a key is pressed. The Windows Frame Server
// loads Onyx.Native.dll in its own process to produce the test-pattern frames.
//
// Prerequisite: register the media source first:
//     regsvr32 Onyx.Native.dll
//
// Windows 11 only (MFCreateVirtualCamera).

#include <windows.h>
#include <mfapi.h>
#include <mfvirtualcamera.h>
#include <wrl.h>
#include <cstdio>

using namespace Microsoft::WRL;

// Must match CLSID_OnyxMediaSource in OnyxGuids.h, in string form.
static const wchar_t* kSourceClsid = L"{6B2E4F1A-9C3D-4A7E-B8F5-2D1C3E4A5B60}";
static const wchar_t* kFriendlyName = L"Onyx Virtual Camera";

static int Fail(const char* what, HRESULT hr)
{
    std::printf("[onyx] %s failed: 0x%08lX\n", what, static_cast<unsigned long>(hr));
    return 1;
}

int wmain()
{
    std::printf("[onyx] starting virtual camera host...\n");

    HRESULT hr = MFStartup(MF_VERSION, MFSTARTUP_FULL);
    if (FAILED(hr)) { return Fail("MFStartup", hr); }

    ComPtr<IMFVirtualCamera> vcam;
    hr = MFCreateVirtualCamera(
        MFVirtualCameraType_SoftwareCameraSource,
        MFVirtualCameraLifetime_Session,
        MFVirtualCameraAccess_CurrentUser,
        kFriendlyName,
        kSourceClsid,
        /*categories*/ nullptr,
        /*categoryCount*/ 0,
        &vcam);
    if (FAILED(hr))
    {
        MFShutdown();
        return Fail("MFCreateVirtualCamera", hr);
    }

    hr = vcam->Start(nullptr);
    if (FAILED(hr))
    {
        vcam->Remove();
        MFShutdown();
        return Fail("IMFVirtualCamera::Start", hr);
    }

    std::printf("[onyx] '%ls' is live. Open OBS / Windows Camera to view.\n", kFriendlyName);
    std::printf("[onyx] press ENTER to stop and remove the camera...\n");
    std::getchar();

    vcam->Stop();
    vcam->Remove();
    MFShutdown();
    std::printf("[onyx] stopped.\n");
    return 0;
}
