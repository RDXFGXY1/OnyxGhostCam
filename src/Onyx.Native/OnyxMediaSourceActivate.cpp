#include "OnyxMediaSourceActivate.h"
#include "OnyxMediaSource.h"
#include "OnyxLog.h"

using namespace Microsoft::WRL;

namespace onyx {

OnyxMediaSourceActivate::OnyxMediaSourceActivate() = default;

HRESULT OnyxMediaSourceActivate::RuntimeClassInitialize()
{
    return MFCreateAttributes(&_attributes, 1);
}

// ---- IMFActivate ----

IFACEMETHODIMP OnyxMediaSourceActivate::ActivateObject(REFIID riid, void** ppv)
{
    onyx::Log("Activate::ActivateObject");
    if (!ppv) { return E_POINTER; }
    if (!_source)
    {
        ComPtr<OnyxMediaSource> source;
        HRESULT hr = MakeAndInitialize<OnyxMediaSource>(&source);
        if (FAILED(hr)) { return hr; }
        _source = source;
    }
    return _source.CopyTo(riid, ppv);
}

IFACEMETHODIMP OnyxMediaSourceActivate::ShutdownObject()
{
    if (_source)
    {
        _source->Shutdown();
        _source.Reset();
    }
    return S_OK;
}

IFACEMETHODIMP OnyxMediaSourceActivate::DetachObject()
{
    _source.Reset();
    return S_OK;
}

// ---- IMFAttributes: forward everything to _attributes ----

IFACEMETHODIMP OnyxMediaSourceActivate::GetItem(REFGUID k, PROPVARIANT* v) { return _attributes->GetItem(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetItemType(REFGUID k, MF_ATTRIBUTE_TYPE* t) { return _attributes->GetItemType(k, t); }
IFACEMETHODIMP OnyxMediaSourceActivate::CompareItem(REFGUID k, REFPROPVARIANT v, BOOL* r) { return _attributes->CompareItem(k, v, r); }
IFACEMETHODIMP OnyxMediaSourceActivate::Compare(IMFAttributes* a, MF_ATTRIBUTES_MATCH_TYPE t, BOOL* r) { return _attributes->Compare(a, t, r); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetUINT32(REFGUID k, UINT32* v) { return _attributes->GetUINT32(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetUINT64(REFGUID k, UINT64* v) { return _attributes->GetUINT64(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetDouble(REFGUID k, double* v) { return _attributes->GetDouble(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetGUID(REFGUID k, GUID* v) { return _attributes->GetGUID(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetStringLength(REFGUID k, UINT32* l) { return _attributes->GetStringLength(k, l); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetString(REFGUID k, LPWSTR v, UINT32 n, UINT32* l) { return _attributes->GetString(k, v, n, l); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetAllocatedString(REFGUID k, LPWSTR* v, UINT32* l) { return _attributes->GetAllocatedString(k, v, l); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetBlobSize(REFGUID k, UINT32* s) { return _attributes->GetBlobSize(k, s); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetBlob(REFGUID k, UINT8* buf, UINT32 n, UINT32* s) { return _attributes->GetBlob(k, buf, n, s); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetAllocatedBlob(REFGUID k, UINT8** buf, UINT32* s) { return _attributes->GetAllocatedBlob(k, buf, s); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetUnknown(REFGUID k, REFIID riid, LPVOID* v) { return _attributes->GetUnknown(k, riid, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetItem(REFGUID k, REFPROPVARIANT v) { return _attributes->SetItem(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::DeleteItem(REFGUID k) { return _attributes->DeleteItem(k); }
IFACEMETHODIMP OnyxMediaSourceActivate::DeleteAllItems() { return _attributes->DeleteAllItems(); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetUINT32(REFGUID k, UINT32 v) { return _attributes->SetUINT32(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetUINT64(REFGUID k, UINT64 v) { return _attributes->SetUINT64(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetDouble(REFGUID k, double v) { return _attributes->SetDouble(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetGUID(REFGUID k, REFGUID v) { return _attributes->SetGUID(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetString(REFGUID k, LPCWSTR v) { return _attributes->SetString(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetBlob(REFGUID k, const UINT8* buf, UINT32 n) { return _attributes->SetBlob(k, buf, n); }
IFACEMETHODIMP OnyxMediaSourceActivate::SetUnknown(REFGUID k, IUnknown* v) { return _attributes->SetUnknown(k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::LockStore() { return _attributes->LockStore(); }
IFACEMETHODIMP OnyxMediaSourceActivate::UnlockStore() { return _attributes->UnlockStore(); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetCount(UINT32* c) { return _attributes->GetCount(c); }
IFACEMETHODIMP OnyxMediaSourceActivate::GetItemByIndex(UINT32 i, GUID* k, PROPVARIANT* v) { return _attributes->GetItemByIndex(i, k, v); }
IFACEMETHODIMP OnyxMediaSourceActivate::CopyAllItems(IMFAttributes* dest) { return _attributes->CopyAllItems(dest); }

}  // namespace onyx
