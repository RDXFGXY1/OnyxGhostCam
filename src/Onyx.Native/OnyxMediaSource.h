#pragma once

#include <windows.h>
#include <mfidl.h>
#include <mfapi.h>
#include <mferror.h>
#include <wrl.h>
#include <ks.h>
#include <ksproxy.h>

#include "OnyxMediaStream.h"

// Onyx software media source (milestone 1b).
//
// The COM object the Windows Frame Server instantiates to back our virtual
// camera. Owns a single video stream serving the test pattern.

namespace onyx {

class OnyxMediaSource
    : public Microsoft::WRL::RuntimeClass<
          Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
          Microsoft::WRL::ChainInterfaces<IMFMediaSourceEx, IMFMediaSource, IMFMediaEventGenerator>,
          IMFGetService,
          IKsControl,
          IMFSampleAllocatorControl>
{
public:
    OnyxMediaSource();

    HRESULT RuntimeClassInitialize();

    // ---- IMFMediaEventGenerator ----
    IFACEMETHODIMP GetEvent(DWORD flags, IMFMediaEvent** ppEvent) override;
    IFACEMETHODIMP BeginGetEvent(IMFAsyncCallback* pCallback, IUnknown* punkState) override;
    IFACEMETHODIMP EndGetEvent(IMFAsyncResult* pResult, IMFMediaEvent** ppEvent) override;
    IFACEMETHODIMP QueueEvent(MediaEventType met, REFGUID guidExtendedType,
                              HRESULT hrStatus, const PROPVARIANT* pvValue) override;

    // ---- IMFMediaSource ----
    IFACEMETHODIMP GetCharacteristics(DWORD* pdwCharacteristics) override;
    IFACEMETHODIMP CreatePresentationDescriptor(IMFPresentationDescriptor** ppPD) override;
    IFACEMETHODIMP Start(IMFPresentationDescriptor* pPD, const GUID* pguidTimeFormat,
                         const PROPVARIANT* pvarStartPos) override;
    IFACEMETHODIMP Stop() override;
    IFACEMETHODIMP Pause() override;
    IFACEMETHODIMP Shutdown() override;

    // ---- IMFMediaSourceEx ----
    IFACEMETHODIMP GetSourceAttributes(IMFAttributes** ppAttributes) override;
    IFACEMETHODIMP GetStreamAttributes(DWORD dwStreamId, IMFAttributes** ppAttributes) override;
    IFACEMETHODIMP SetD3DManager(IUnknown* pManager) override;

    // ---- IMFGetService ----
    IFACEMETHODIMP GetService(REFGUID guidService, REFIID riid, LPVOID* ppvObject) override;

    // ---- IKsControl ----
    IFACEMETHODIMP KsProperty(PKSPROPERTY, ULONG, LPVOID, ULONG, ULONG*) override;
    IFACEMETHODIMP KsMethod(PKSMETHOD, ULONG, LPVOID, ULONG, ULONG*) override;
    IFACEMETHODIMP KsEvent(PKSEVENT, ULONG, LPVOID, ULONG, ULONG*) override;

    // ---- IMFSampleAllocatorControl ----
    IFACEMETHODIMP SetDefaultAllocator(DWORD dwOutputStreamID, IUnknown* pAllocator) override;
    IFACEMETHODIMP GetAllocatorUsage(DWORD dwOutputStreamID, DWORD* pdwInputStreamID,
                                     MFSampleAllocatorUsage* peUsage) override;

private:
    HRESULT CheckShutdown() const;
    HRESULT CreatePresentationDescriptorLocked(IMFPresentationDescriptor** ppPD);

    Microsoft::WRL::Wrappers::SRWLock          _lock;
    Microsoft::WRL::ComPtr<IMFMediaEventQueue> _eventQueue;
    Microsoft::WRL::ComPtr<IMFAttributes>      _attributes;
    Microsoft::WRL::ComPtr<OnyxMediaStream>    _stream;
    Microsoft::WRL::ComPtr<IMFStreamDescriptor> _streamDescriptor;

    bool _shutdown = false;
    bool _started = false;
};

}  // namespace onyx
