// Onyx virtual-camera host (milestone 1c / MSIX identity test).
//
// Creates the Onyx virtual camera from our registered media source (by CLSID),
// starts it, and stays alive so consumers can stream. The Windows Frame Server
// loads Onyx.Native.dll in its own process to produce the frames.
//
// Prerequisites:
//   * register the media source:   regsvr32 Onyx.Native.dll   (HKLM, via deploy-vcam.ps1)
//   * run this host WITH package identity (MSIX) so it can obtain camera
//     consent - MFCreateVirtualCamera requires it before the Frame Server will
//     stream. See pkg-vcam.ps1.
//
// Windows 11 only.

#include <windows.h>
#include <mfapi.h>
#include <mfvirtualcamera.h>
#include <appmodel.h>
#include <wrl.h>
#include <cstdio>
#include <string>

using namespace Microsoft::WRL;

static const wchar_t* kSourceClsid = L"{6B2E4F1A-9C3D-4A7E-B8F5-2D1C3E4A5B60}";
static const wchar_t* kFriendlyName = L"Onyx Virtual Camera";

static int Fail(const char* what, HRESULT hr)
{
    std::printf("[onyx] %s failed: 0x%08lX\n", what, static_cast<unsigned long>(hr));
    return 1;
}

static void ReportIdentity()
{
    UINT32 len = 0;
    LONG rc = GetCurrentPackageFullName(&len, nullptr);
    if (rc == APPMODEL_ERROR_NO_PACKAGE)
    {
        std::printf("[onyx] package identity: NONE (unpackaged - camera consent unavailable)\n");
        return;
    }
    std::wstring buf(len, L'\0');
    if (GetCurrentPackageFullName(&len, buf.data()) == ERROR_SUCCESS)
    {
        std::wprintf(L"[onyx] package identity: %ls\n", buf.c_str());
    }
}

int wmain()
{
    std::printf("[onyx] starting virtual camera host...\n");
    ReportIdentity();

    HRESULT hr = MFStartup(MF_VERSION, MFSTARTUP_FULL);
    if (FAILED(hr)) { return Fail("MFStartup", hr); }

    ComPtr<IMFVirtualCamera> vcam;
    hr = MFCreateVirtualCamera(
        MFVirtualCameraType_SoftwareCameraSource,
        MFVirtualCameraLifetime_Session,
        MFVirtualCameraAccess_CurrentUser,
        kFriendlyName,
        kSourceClsid,
        nullptr, 0,
        &vcam);
    if (FAILED(hr)) { MFShutdown(); return Fail("MFCreateVirtualCamera", hr); }

    hr = vcam->Start(nullptr);
    if (FAILED(hr)) { vcam->Remove(); MFShutdown(); return Fail("IMFVirtualCamera::Start", hr); }

    std::printf("[onyx] '%ls' is live. Open OBS / Windows Camera / Discord to view.\n", kFriendlyName);
    std::printf("[onyx] staying alive up to 10 min (or signal event OnyxVCamStop)...\n");

    // Stay alive headless (no console stdin when launched packaged). A named
    // event lets a script stop us cleanly; otherwise time out after 10 minutes.
    HANDLE stop = CreateEventW(nullptr, TRUE, FALSE, L"Global\\OnyxVCamStop");
    WaitForSingleObject(stop, 600000);
    if (stop) { CloseHandle(stop); }

    vcam->Stop();
    vcam->Remove();
    MFShutdown();
    std::printf("[onyx] stopped.\n");
    return 0;
}
