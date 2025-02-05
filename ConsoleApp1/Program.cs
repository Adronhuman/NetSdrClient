// See https://aka.ms/new-console-template for more information
using NetSdrClient.CommandBuilder;
using NetSdrClient.Models.Enums;
using System.Collections;

Console.WriteLine("Hello, World!");

var shit = NetSDRCommandBuilder.SetReceiverStateMessage(true, true, CaptureMode.Fifo16Bit, 10);

foreach (byte b in shit)
{
    Console.Write("0x{0:X2} ", b);
}