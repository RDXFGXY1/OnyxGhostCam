#define ONYXNATIVE_EXPORTS
#include "VirtualCamera.h"

// NOTE: Stub implementation. The real body uses MFCreateVirtualCamera plus a
// registered MF media source that first emits a test pattern (milestone #1),
// then reads processed frames from a shared-memory bridge fed by Onyx.Core.
//
// Kept as a compilable stub so the solution builds once the C++ workload +
// Windows SDK are installed. Fill in once `mfvirtualcamera.h` is available.

extern "C" {

long OnyxVirtualCameraStart()
{
    // TODO: MFStartup + MFCreateVirtualCamera(...) + Start().
    return 0;  // S_OK placeholder
}

long OnyxVirtualCameraStop()
{
    // TODO: Stop() + Remove() + MFShutdown.
    return 0;  // S_OK placeholder
}

}  // extern "C"
