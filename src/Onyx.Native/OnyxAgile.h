#pragma once

#include <windows.h>
#include <objidl.h>
#include <wrl.h>

// Agility helper (rebuild on the proven Frame Server pattern).
//
// The Windows Frame Server calls a hosted media source from multiple COM
// apartments. A plain ClassicCom WRL object is apartment-bound, so cross-
// apartment calls (notably IMFMediaSource::Start) fail with RPC_S_CALL_FAILED
// and never reach our code. Aggregating the Free-Threaded Marshaler makes the
// object agile, which every real MF software source must be.
//
// Usage:
//   1. Add  Microsoft::WRL::CloakedIid<IMarshal>  to the RuntimeClass list.
//   2. Put  ONYX_AGILE_MEMBERS()  in the class body.
//   3. Call InitAgile() from RuntimeClassInitialize.

#define ONYX_AGILE_MEMBERS()                                                                     \
    Microsoft::WRL::ComPtr<IMarshal> _ftm;                                                        \
    HRESULT InitAgile()                                                                           \
    {                                                                                             \
        return CoCreateFreeThreadedMarshaler(static_cast<IMarshal*>(this), &_ftm);                \
    }                                                                                             \
    IFACEMETHODIMP GetUnmarshalClass(REFIID riid, void* pv, DWORD ctx, void* pvCtx,               \
                                     DWORD flags, CLSID* pCid) override                            \
    { return _ftm->GetUnmarshalClass(riid, pv, ctx, pvCtx, flags, pCid); }                        \
    IFACEMETHODIMP GetMarshalSizeMax(REFIID riid, void* pv, DWORD ctx, void* pvCtx,               \
                                     DWORD flags, DWORD* pSize) override                           \
    { return _ftm->GetMarshalSizeMax(riid, pv, ctx, pvCtx, flags, pSize); }                       \
    IFACEMETHODIMP MarshalInterface(IStream* s, REFIID riid, void* pv, DWORD ctx, void* pvCtx,    \
                                    DWORD flags) override                                          \
    { return _ftm->MarshalInterface(s, riid, pv, ctx, pvCtx, flags); }                            \
    IFACEMETHODIMP UnmarshalInterface(IStream* s, REFIID riid, void** ppv) override               \
    { return _ftm->UnmarshalInterface(s, riid, ppv); }                                            \
    IFACEMETHODIMP ReleaseMarshalData(IStream* s) override { return _ftm->ReleaseMarshalData(s); }\
    IFACEMETHODIMP DisconnectObject(DWORD r) override { return _ftm->DisconnectObject(r); }
