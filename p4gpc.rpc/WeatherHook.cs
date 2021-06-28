using p4gpc.rpc.Configuration;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

namespace p4gpc.rpc
{
    class WeatherHook
    {
        // Location of the function that sets weather (should scan for it later on)
        private string location = "P4G.exe+22ACAA88";

        // For manipulating Weather Hook.
        private IAsmHook _asmHook;

        // For calling C# code from ASM.
        private IReverseWrapper<GetWeatherFunction> _reverseWrapper;

        // Provides logging functionality.
        private ILogger _logger;


        public WeatherHook(ILogger logger, IReloadedHooks hooks, int baseAddress)
        {
            long weatherCodeAddress = baseAddress + 581741192;
            _logger = logger;
            _logger.WriteLine("[RPC] Weather address: " + weatherCodeAddress.ToString());

            string[] function =
{
                $"use32",
                // Not always necessary but good practice;
                // just in case the parent function doesn't preserve them.
                $"{hooks.Utilities.PushCdeclCallerSavedRegisters()}",
                $"{hooks.Utilities.GetAbsoluteCallMnemonics(GetWeather, out _reverseWrapper)}",
                $"{hooks.Utilities.PopCdeclCallerSavedRegisters()}",
            };

            _asmHook = hooks.CreateAsmHook(function, weatherCodeAddress, AsmHookBehaviour.ExecuteFirst).Activate();

        }



        private void GetWeather(int eax)
        {
            _logger.WriteLine("[RPC] Getting the weather");
            _logger.WriteLine(eax.ToString());
        }

        [Function(Register.eax, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void GetWeatherFunction(int eax);
    }
}
