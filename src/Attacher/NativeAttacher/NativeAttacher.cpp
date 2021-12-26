#include <iostream>
#include "MetaHost.h"
#include "objidlbase.h"
#include "psapi.h"
#include <Windows.h> // Todo: make it cross platform
#include <tchar.h>

#pragma comment(lib, "mscoree.lib")

#define TRY(x)  { \
					HRESULT hr; \
					if (FAILED(hr = x)) { \
						printf("Failed: '%s'. Reason: 0x%x.\n", #x, hr); \
						return; \
					} \
				}

HRESULT GetProcessByID(const DWORD id, HANDLE &processHandle)
{
	//processHandle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, FALSE, id);
	processHandle = OpenProcess(PROCESS_ALL_ACCESS, FALSE, id);
	if (processHandle == NULL) {
		return E_POINTER;
	} else {
		return S_OK;
	}
}

HRESULT GetProcessByName(const TCHAR element[], HANDLE &processHandle)
{
    DWORD aProcesses[2048], cbNeeded, cProcesses;
    if (!EnumProcesses(aProcesses, sizeof(aProcesses), &cbNeeded))
        return NULL;

    // Calculate how many process identifiers were returned.
	cProcesses = cbNeeded / sizeof(DWORD);

    // Print the name and process identifier for each process.
    for (unsigned int i = 0; i < cProcesses; i++)
    {
        DWORD dwProcessID = aProcesses[i];

        // Get a handle to the process.
		if (FAILED(GetProcessByID(dwProcessID, processHandle))) {
			//wprintf(L"Failing getting process handle for PID: %d\n", dwProcessID);
			continue;
		}

		TCHAR szEachProcessName[MAX_PATH] = TEXT("<unknown>");
		HMODULE hMod;
		DWORD cbNeeded;

		if (!EnumProcessModules(processHandle, &hMod, sizeof(hMod), &cbNeeded)) {
			//wprintf(L"Failing enum PID: %d. Error code: %d\n", dwProcessID, GetLastError());
			continue;
		}

		// Get the process name.
		if (!GetModuleBaseName(processHandle, hMod, szEachProcessName, sizeof(szEachProcessName) / sizeof(TCHAR))) {
			//wprintf(L"Failing getting name for PID: %d. Error code: %d\n", dwProcessID, GetLastError());
			continue;
		}

		//printf("Process name: %s\n", szEachProcessName);

        // if they dont match, exit. otherwise get this party started
        if (_tcscmp(element, szEachProcessName) == 0) {
            return S_OK;
        }

        CloseHandle(processHandle);
    }

    return E_POINTER;
}

void AttachToProcess(LPCWSTR sz_runtimeVersion, HANDLE handle) {
	ICLRMetaHost* pMetaHost = NULL;
	ICLRRuntimeInfo* pRuntimeInfo = NULL;
	ICLRProfiling* pClrProfiling = NULL;

	TRY(CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (VOID**)&pMetaHost));
	TRY(pMetaHost->GetRuntime(sz_runtimeVersion, IID_ICLRRuntimeInfo, (VOID**)&pRuntimeInfo));

	//ICLRRuntimeHost* runtimeHost = NULL;
	//TRY(pRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (LPVOID*)&runtimeHost));
	//// Start runtime, and load our assembly
	//runtimeHost->Start();

	//BOOL isStarted;
	//DWORD startedFlags;
	//TRY(pRuntimeInfo->IsStarted(&isStarted, &startedFlags));
	//if (!isStarted) {
	//	printf("Runtime is not started!\n");
	//	return;
	//}

	BOOL isLoadable;
	TRY(pRuntimeInfo->IsLoadable(&isLoadable));
	if (!isLoadable) {
		printf("Runtime is not loadable!\n");
		return;
	}

	//BOOL isLoaded;
	//TRY(pRuntimeInfo->IsLoaded(handle, &isLoaded));
	//if (!isLoaded) {
	//	printf("Process is not loaded in this runtime!\n");
	//	return;
	//}

	TRY(pRuntimeInfo->GetInterface(CLSID_CLRProfiling, IID_ICLRProfiling, (LPVOID*)&pClrProfiling));

	DWORD id = GetProcessId(handle);

	CLSID clsidProfiler;
	TRY(CLSIDFromString(L"{BD097ED8-733E-43FE-8ED7-A95FF9A8448C}", (LPCLSID)&clsidProfiler));

	auto path = L"C:\\Users\\oginiaux\\Projects\\traceman\\src\\x64\\Debug\\Profiler.Windows.dll";
	LPVOID pvClientData = NULL;
	DWORD cbClientData = 0;

	TRY(pClrProfiling->AttachProfiler(id, 10000, &clsidProfiler, path, pvClientData, cbClientData));
}

void PrintRuntimes(IEnumUnknown* &enumRuntimes) {
	IUnknown* enumRuntime = NULL;
	ICLRRuntimeInfo* runtimeInfo = NULL;
	LPWSTR frameworkName = (LPWSTR)LocalAlloc(LPTR, 2048);
	DWORD bytes = 2048, result = 0;

	while (enumRuntimes->Next(1, &enumRuntime, 0) == S_OK) {
		TRY(enumRuntime->QueryInterface<ICLRRuntimeInfo>(&runtimeInfo));
		TRY(runtimeInfo->GetVersionString(frameworkName, &bytes));
		wprintf(L"- %s\n", frameworkName);
	}
}

void Attach() {
	// Get managed process handle
	HANDLE processHandle;
	TRY(GetProcessByName(TEXT("Fibonacci.exe"), processHandle));
	//TRY(GetProcessByID(136620, processHandle));

	if (processHandle == NULL) {
		printf("Failed getting process handle\n");
	}

	// Get metahost
	HMODULE hModule = LoadLibrary(TEXT("mscoree.dll"));
	CreateInterfaceFnPtr createInterface = (CreateInterfaceFnPtr)GetProcAddress(hModule, "CreateInterface");
	ICLRMetaHost* pMetaHost = NULL;
	TRY(createInterface(CLSID_CLRMetaHost, IID_ICLRMetaHost, (LPVOID*)&pMetaHost));

	// Get some runtime information
	IEnumUnknown* enumLoadedRuntimes = NULL;
	IEnumUnknown* enumInstalledRuntimes = NULL;
	TRY(pMetaHost->EnumerateLoadedRuntimes(processHandle, &enumLoadedRuntimes));
	TRY(pMetaHost->EnumerateInstalledRuntimes(&enumInstalledRuntimes));

	printf("Loaded Runtimes:\n");
	PrintRuntimes(enumLoadedRuntimes);

	printf("Installed Runtimes:\n");
	PrintRuntimes(enumInstalledRuntimes);

	//AttachToProcess(L"v2.0.50727", processHandle);
	AttachToProcess(L"v4.0.30319", processHandle);
}

int main()
{
	Attach();
	std::cin.get();
}
