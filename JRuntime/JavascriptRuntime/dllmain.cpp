// dllmain.cpp : Definiert den Einstiegspunkt für die DLL-Anwendung.
#include "stdafx.h"
#include <fstream>
#include "JUi.h"
/*
TODO:
-> Add __thiscall hooking and calling
-> Implement a "pointer" type?! instead of using int
-> Add a graphic library and export a ui namespace to make overlay drawing possible
*/

DWORD WINAPI Main(LPVOID arg)
{
	JUi::Create();
	return 0;
}

BOOL APIENTRY DllMain(HMODULE hModule,
	DWORD  ul_reason_for_call,
	LPVOID lpReserved
)
{
	switch (ul_reason_for_call)
	{
	case DLL_PROCESS_ATTACH:
		CreateThread(0, 0, &Main, 0, 0, 0);
	case DLL_THREAD_ATTACH:
	case DLL_THREAD_DETACH:
	case DLL_PROCESS_DETACH:
		break;
	}
	return TRUE;
}

