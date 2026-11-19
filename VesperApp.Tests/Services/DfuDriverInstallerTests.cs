using VesperApp.Services;
using Xunit;

namespace VesperApp.Tests.Services;

/// <summary>
/// The ST bootloader (USB DFU) driver step: the pnputil call and the reading of its driver list.
/// The install itself needs administrator rights and is checked by hand (release test).
/// </summary>
public class DfuDriverInstallerTests
{
    [Fact]
    public void PnpUtil_A32BitProcessOn64BitWindows_UsesSysnative()
    {
        Assert.EndsWith(@"System32\pnputil.exe", DfuDriverInstaller.PnpUtilPath(@"C:\Windows", is64BitOs: true, is64BitProcess: true).Replace('/', '\\'));
        Assert.EndsWith(@"Sysnative\pnputil.exe", DfuDriverInstaller.PnpUtilPath(@"C:\Windows", is64BitOs: true, is64BitProcess: false).Replace('/', '\\'));
        Assert.EndsWith(@"System32\pnputil.exe", DfuDriverInstaller.PnpUtilPath(@"C:\Windows", is64BitOs: false, is64BitProcess: false).Replace('/', '\\'));
    }

    [Fact]
    public void InstallArguments_QuoteThePath()
    {
        Assert.Equal("/add-driver \"C:\\Users\\A B\\AppData\\Local\\VesperApp\\current\\drivers\\st-dfu\\STM32Bootloader.inf\" /install",
            DfuDriverInstaller.InstallArguments(@"C:\Users\A B\AppData\Local\VesperApp\current\drivers\st-dfu\STM32Bootloader.inf"));
    }

    [Fact]
    public void IsListed_FindsThePackageByItsFileName_WhateverTheLanguageOfTheLabels()
    {
        const string english = """
            Published Name:     oem170.inf
            Original Name:      stm32bootloader.inf
            Provider Name:      STMicroelectronics
            Class Name:         USBDevice
            """;
        const string german = """
            Veröffentlichter Name:     oem12.inf
            Originalname:      STM32Bootloader.inf
            Anbietername:      STMicroelectronics
            """;
        const string others = """
            Published Name:     oem214.inf
            Original Name:      stlink_dbg_winusb.inf
            Provider Name:      STMicroelectronics
            """;

        Assert.True(DfuDriverInstaller.IsListed(english));
        Assert.True(DfuDriverInstaller.IsListed(german));
        Assert.False(DfuDriverInstaller.IsListed(others));
        Assert.False(DfuDriverInstaller.IsListed(string.Empty));
    }

    [Theory]
    [InlineData(0, true)]       // added and installed
    [InlineData(259, true)]     // added; no bootloader connected to install it on
    [InlineData(3010, true)]    // added; Windows asks for a restart
    [InlineData(1, false)]
    [InlineData(5, false)]      // access denied
    public void ExitCodes(int exitCode, bool success) =>
        Assert.Equal(success, DfuDriverInstaller.IsSuccessExitCode(exitCode));

    [Fact]
    public void InstallRun_IsRecognisedByItsSwitch()
    {
        Assert.True(DfuDriverInstaller.IsInstallRun(new[] { "--install-dfu-driver" }));
        Assert.True(DfuDriverInstaller.IsInstallRun(new[] { "--INSTALL-DFU-DRIVER" }));
        Assert.False(DfuDriverInstaller.IsInstallRun(new[] { "--veloapp-install", "1.2.2" }));
        Assert.False(DfuDriverInstaller.IsInstallRun(Array.Empty<string>()));
    }

    /// <summary>The package is part of the build output on Windows, unchanged: the catalog signs the .inf byte for byte.</summary>
    [Fact]
    public void DriverPackage_IsShippedWithTheWindowsBuild()
    {
        if (!OperatingSystem.IsWindows()) return;

        string dir = Path.Combine(AppContext.BaseDirectory, "drivers", "st-dfu");
        byte[] inf = File.ReadAllBytes(Path.Combine(dir, "STM32Bootloader.inf"));
        Assert.True(File.Exists(Path.Combine(dir, "stm32bootloader.cat")));

        Assert.Equal(new byte[] { 0xFF, 0xFE }, inf[..2]);              // UTF-16 LE as ST ships it: not normalised by git
        string text = System.Text.Encoding.Unicode.GetString(inf);
        Assert.Contains("VID_0483&PID_DF11", text);
        Assert.Contains("CatalogFile = STM32Bootloader.cat", text);
    }
}
