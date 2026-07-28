// COM plumbing for the Onyx media source DLL (milestone 1c).
//
// Exposes OnyxMediaSource as an in-proc COM server so the Windows Frame Server
// can CoCreateInstance it by CLSID. Also self-registers the CLSID (per-user, so
// no admin is needed) via DllRegisterServer / regsvr32.

#include <windows.h>
#include <mfapi.h>
#include <wrl.h>
#include <string>
#include <new>

// <initguid.h> before OnyxGuids.h makes THIS translation unit emit the actual
// bytes for CLSID_OnyxMediaSource (DEFINE_GUID only declares it elsewhere).
#include <initguid.h>
#include "OnyxGuids.h"
#include "OnyxMediaSourceActivate.h"

using namespace Microsoft::WRL;

namespace {

HMODULE g_hModule = nullptr;
long    g_lockCount = 0;

// Machine-wide COM registration (HKLM) so the Windows Camera Frame Server
// service — which runs under a different account and cannot read the current
// user's HKCU hive — can CoCreateInstance our source. Requires admin regsvr32.
constexpr wchar_t kClsidKeyFmt[] =
    L"Software\\Classes\\CLSID\\{6B2E4F1A-9C3D-4A7E-B8F5-2D1C3E4A5B60}";
constexpr wchar_t kInprocKeyFmt[] =
    L"Software\\Classes\\CLSID\\{6B2E4F1A-9C3D-4A7E-B8F5-2D1C3E4A5B60}\\InprocServer32";

HRESULT SetRegValue(HKEY root, const wchar_t* subkey, const wchar_t* name, const wchar_t* value)
{
    HKEY key = nullptr;
    LONG rc = RegCreateKeyExW(root, subkey, 0, nullptr, REG_OPTION_NON_VOLATILE,
                              KEY_WRITE, nullptr, &key, nullptr);
    if (rc != ERROR_SUCCESS) { return HRESULT_FROM_WIN32(rc); }
    const DWORD bytes = static_cast<DWORD>((wcslen(value) + 1) * sizeof(wchar_t));
    rc = RegSetValueExW(key, name, 0, REG_SZ,
                        reinterpret_cast<const BYTE*>(value), bytes);
    RegCloseKey(key);
    return HRESULT_FROM_WIN32(rc);
}

// Minimal IClassFactory producing OnyxMediaSource instances.
class OnyxClassFactory : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IClassFactory>
{
public:
    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (outer) { return CLASS_E_NOAGGREGATION; }
        if (!ppv)  { return E_POINTER; }
        *ppv = nullptr;

        // Return an activation object; the Frame Server calls ActivateObject()
        // on it to obtain the actual OnyxMediaSource.
        ComPtr<onyx::OnyxMediaSourceActivate> activate;
        HRESULT hr = MakeAndInitialize<onyx::OnyxMediaSourceActivate>(&activate);
        if (FAILED(hr)) { return hr; }
        return activate.CopyTo(riid, ppv);
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock) { InterlockedIncrement(&g_lockCount); }
        else      { InterlockedDecrement(&g_lockCount); }
        return S_OK;
    }
};

}  // namespace

BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hModule = hInstance;
        DisableThreadLibraryCalls(hInstance);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (!ppv) { return E_POINTER; }
    *ppv = nullptr;
    if (rclsid != CLSID_OnyxMediaSource) { return CLASS_E_CLASSNOTAVAILABLE; }

    ComPtr<OnyxClassFactory> factory = Make<OnyxClassFactory>();
    if (!factory) { return E_OUTOFMEMORY; }
    return factory.CopyTo(riid, ppv);
}

STDAPI DllCanUnloadNow()
{
    return (g_lockCount == 0) ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    wchar_t path[MAX_PATH] = {};
    if (GetModuleFileNameW(g_hModule, path, MAX_PATH) == 0)
    {
        return HRESULT_FROM_WIN32(GetLastError());
    }

    HRESULT hr = SetRegValue(HKEY_LOCAL_MACHINE, kClsidKeyFmt, nullptr, L"Onyx Media Source");
    if (FAILED(hr)) { return hr; }
    hr = SetRegValue(HKEY_LOCAL_MACHINE, kInprocKeyFmt, nullptr, path);
    if (FAILED(hr)) { return hr; }
    hr = SetRegValue(HKEY_LOCAL_MACHINE, kInprocKeyFmt, L"ThreadingModel", L"Both");
    return hr;
}

STDAPI DllUnregisterServer()
{
    RegDeleteKeyW(HKEY_LOCAL_MACHINE, kInprocKeyFmt);
    RegDeleteKeyW(HKEY_LOCAL_MACHINE, kClsidKeyFmt);
    return S_OK;
}
