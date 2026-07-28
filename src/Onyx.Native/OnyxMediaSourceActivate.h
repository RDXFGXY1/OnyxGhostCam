#pragma once

#include <windows.h>
#include <mfidl.h>
#include <mfapi.h>
#include <wrl.h>
#include "OnyxAgile.h"

// Activation object for the Onyx media source (milestone 1c fix).
//
// The Windows Frame Server CoCreateInstances our registered CLSID expecting an
// IMFActivate, then calls ActivateObject() to obtain the real media source.
// This wraps that indirection. All IMFAttributes calls forward to an internal
// attribute store.

namespace onyx {

class OnyxMediaSourceActivate
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          Microsoft::WRL::ChainInterfaces<IMFActivate, IMFAttributes>,
          Microsoft::WRL::CloakedIid<IMarshal>>
{
public:
    OnyxMediaSourceActivate();
    HRESULT RuntimeClassInitialize();

    // ---- IMFActivate ----
    IFACEMETHODIMP ActivateObject(REFIID riid, void** ppv) override;
    IFACEMETHODIMP ShutdownObject() override;
    IFACEMETHODIMP DetachObject() override;

    // ---- IMFAttributes (forwarded to _attributes) ----
    IFACEMETHODIMP GetItem(REFGUID k, PROPVARIANT* v) override;
    IFACEMETHODIMP GetItemType(REFGUID k, MF_ATTRIBUTE_TYPE* t) override;
    IFACEMETHODIMP CompareItem(REFGUID k, REFPROPVARIANT v, BOOL* r) override;
    IFACEMETHODIMP Compare(IMFAttributes* a, MF_ATTRIBUTES_MATCH_TYPE t, BOOL* r) override;
    IFACEMETHODIMP GetUINT32(REFGUID k, UINT32* v) override;
    IFACEMETHODIMP GetUINT64(REFGUID k, UINT64* v) override;
    IFACEMETHODIMP GetDouble(REFGUID k, double* v) override;
    IFACEMETHODIMP GetGUID(REFGUID k, GUID* v) override;
    IFACEMETHODIMP GetStringLength(REFGUID k, UINT32* l) override;
    IFACEMETHODIMP GetString(REFGUID k, LPWSTR v, UINT32 n, UINT32* l) override;
    IFACEMETHODIMP GetAllocatedString(REFGUID k, LPWSTR* v, UINT32* l) override;
    IFACEMETHODIMP GetBlobSize(REFGUID k, UINT32* s) override;
    IFACEMETHODIMP GetBlob(REFGUID k, UINT8* buf, UINT32 n, UINT32* s) override;
    IFACEMETHODIMP GetAllocatedBlob(REFGUID k, UINT8** buf, UINT32* s) override;
    IFACEMETHODIMP GetUnknown(REFGUID k, REFIID riid, LPVOID* v) override;
    IFACEMETHODIMP SetItem(REFGUID k, REFPROPVARIANT v) override;
    IFACEMETHODIMP DeleteItem(REFGUID k) override;
    IFACEMETHODIMP DeleteAllItems() override;
    IFACEMETHODIMP SetUINT32(REFGUID k, UINT32 v) override;
    IFACEMETHODIMP SetUINT64(REFGUID k, UINT64 v) override;
    IFACEMETHODIMP SetDouble(REFGUID k, double v) override;
    IFACEMETHODIMP SetGUID(REFGUID k, REFGUID v) override;
    IFACEMETHODIMP SetString(REFGUID k, LPCWSTR v) override;
    IFACEMETHODIMP SetBlob(REFGUID k, const UINT8* buf, UINT32 n) override;
    IFACEMETHODIMP SetUnknown(REFGUID k, IUnknown* v) override;
    IFACEMETHODIMP LockStore() override;
    IFACEMETHODIMP UnlockStore() override;
    IFACEMETHODIMP GetCount(UINT32* c) override;
    IFACEMETHODIMP GetItemByIndex(UINT32 i, GUID* k, PROPVARIANT* v) override;
    IFACEMETHODIMP CopyAllItems(IMFAttributes* dest) override;

    // ---- IMarshal (agility via free-threaded marshaler) ----
    ONYX_AGILE_MEMBERS()

private:
    Microsoft::WRL::ComPtr<IMFAttributes>   _attributes;
    Microsoft::WRL::ComPtr<IMFMediaSource>  _source;  // cached activated source
};

}  // namespace onyx
