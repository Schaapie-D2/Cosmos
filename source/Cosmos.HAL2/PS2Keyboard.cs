﻿//#define COSMOSDEBUG

using System;

using Cosmos.Core;
using Cosmos.Debug.Kernel;

namespace Cosmos.HAL
{
    /// <summary>
    /// This class describes the PS/2 keyboard.
    /// </summary>
    public class PS2Keyboard : KeyboardBase
    {
        enum Command : byte
        {
            SetLEDs = 0xED,
            GetOrSetScanCodeSet = 0xF0,
            EnableScanning = 0xF4,
            DisableScanning = 0xF5,
            Reset = 0xFF
        }

        public byte PS2Port { get; }

        public byte ScanCodeSet { get; private set; }
        private bool scanCodeSet2Skip = false;

        private PS2Controller mPS2Controller = Global.PS2Controller;
        private Debugger mDebugger = new("PS2Keyboard");

        internal PS2Keyboard(byte aPort)
        {
            PS2Port = aPort;
        }

        public override void Initialize()
        {
            SendCommand(Command.Reset);
            mPS2Controller.WaitForDeviceReset();

            //VMWare doesn't support the Get/SetScanCode command
            //mDebugger.SendInternal("(PS/2 Keyboard) Current scan code set: " + GetScanCodeSet());
            //SetScanCodeSet(1);
            //mDebugger.SendInternal("(PS/2 Keyboard) Current scan code set: " + GetScanCodeSet());

            INTs.SetIrqHandler(1, HandleIRQ);

            SendCommand(Command.EnableScanning);

            Global.debugger.SendInternal("(PS/2 Keyboard) Initialized");

            UpdateLeds();
            Global.debugger.SendInternal("(PS/2 Keyboard) Leds updated");
            ScanCodeSet = GetScanCodeSet();
        }

        private void HandleIRQ(ref INTs.IRQContext aContext)
        {
            byte code = IOPort.Read8(Cosmos.Core.IOGroup.PS2Controller.Data);
            bool released = false;

            if (ScanCodeSet == 1)
            {
                released = (code & 0x80) == 0x80;
                if (released) code ^= 0x80;
            }
            else if (ScanCodeSet == 2)
            {
                if (code == 0xF0)
                {
                    scanCodeSet2Skip = true;
                    return;
                }

                released = scanCodeSet2Skip;
                scanCodeSet2Skip = false;
            }
            else // VMWare
            {
                released = (code & 0x80) != 0;
                if (released) code ^= 0x80;
            }

            OnKeyPressed?.Invoke(code, released);
        }

        /// <summary>
        /// Update keyboard LEDs.
        /// </summary>
        public override void UpdateLeds()
        {
            // for now, lets not do this..
            //IO.Port60.Byte = 0xED;
            //while ((new IOPort(0x64).Byte & 2) != 0)
            //{
            //}
            //var led_status = (Global.ScrollLock ? 1 : 0) | ((Global.NumLock ? 1 : 0) << 1) | ((Global.CapsLock ? 1 : 0) << 2);
            //IO.Port60.Byte = (byte)led_status;
            //while ((new IOPort(0x64).Byte & 2) != 0)
            //{
            //}

            // Updated Code (not tested):
            //var xLEDs = (byte)(Global.ScrollLock ? 1 : 0) | ((Global.NumLock ? 1 : 0) << 1) | ((Global.CapsLock ? 1 : 0) << 2);
            //SendCommand(Command.SetLEDs, xLEDs);
        }

        /// <summary>
        /// Gets the current keyboard scan code set.
        /// </summary>
        /// <returns>Returns the current scan code set.</returns>
        public byte GetScanCodeSet()
        {
            SendCommand(Command.GetOrSetScanCodeSet, 0);
            return mPS2Controller.ReadByteAfterAck();
        }

        /// <summary>
        /// Sets the scan code set.
        /// </summary>
        /// <param name="aScanCodeSet">The scan code set to set. Can be 1, 2 or 3.</param>
        public void SetScanCodeSet(byte aScanCodeSet)
        {
            if (aScanCodeSet == 1 || aScanCodeSet == 2 || aScanCodeSet == 3)
            {
                SendCommand(Command.GetOrSetScanCodeSet, aScanCodeSet);
            }
            else
            {
                throw new Exception("(PS/2 Keyboard) Scan code set '" + aScanCodeSet + "' doesn't exist");
            }
        }

        private void SendCommand(Command aCommand, byte? aByte = null)
        {
            mDebugger.SendInternal("(PS/2 Keyboard) Sending command:");
            mDebugger.SendInternal("Command:");
            mDebugger.SendInternal((byte)aCommand);

            if (PS2Port == 2)
            {
                mPS2Controller.PrepareSecondPortWrite();
            }

            mPS2Controller.WaitToWrite();
            IOPort.Write8(Cosmos.Core.IOGroup.PS2Controller.Data, (byte)aCommand);

            mPS2Controller.WaitForAck();

            mDebugger.SendInternal("Command sent.");

            if (aByte.HasValue)
            {
                mDebugger.SendInternal("(PS/2 Keyboard) Sending byte after command:");
                mDebugger.SendInternal("Byte value:");
                mDebugger.SendInternal(aByte.Value);

                if (PS2Port == 2)
                {
                    mPS2Controller.PrepareSecondPortWrite();
                }

                mPS2Controller.WaitToWrite();
                IOPort.Write8(Cosmos.Core.IOGroup.PS2Controller.Data, aByte.Value);

                mPS2Controller.WaitForAck();
            }
        }
    }
}