mod profilers;
mod report;
mod interop;
mod macros;
mod utils;

#[macro_use]
extern crate log;

// Create function to list and attach profilers
register!(
    GCSurvivorsProfiler,
    ExceptionsProfiler,
    AllocationByClassProfiler,
    MemoryLeakProfiler,
    RuntimePauseProfiler,
    CpuHotpathProfiler);

static mut invokations: u32 = 0;

// Actual COM entry point
#[no_mangle]
unsafe extern "system" fn DllGetClassObject(rclsid: ffi::REFCLSID, riid: ffi::REFIID, ppv: *mut ffi::LPVOID) -> ffi::HRESULT
{
    invokations += 1;
    
    debug!("[profiler] Entered DllGetClassObject. Invokations: {}", invokations);

    if ppv.is_null() {
        return ffi::E_FAIL;
    }
    
    return attach(rclsid, riid, ppv);
}