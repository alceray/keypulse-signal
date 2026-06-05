using KeyPulse.Services;

namespace KeyPulse.Tests.Services;

/// <summary>
/// Pure helpers backing the auto-update flow: release-asset selection, checksum parsing, SHA-256
/// verification, and version comparison. The live download / silent install / Restart-Manager relaunch /
/// UAC behavior is Windows- and network-bound and is verified manually, not here.
/// </summary>
public class UpdateServiceHelpersTests
{
    // SHA-256 of the empty input and of "abc" — fixed, well-known vectors.
    private const string EmptyHashLower = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string EmptyHashUpper = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
    private const string AbcHashLower = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private static readonly byte[] AbcBytes = [0x61, 0x62, 0x63];

    private const string InstallerName = "KeyPulse-Signal-Setup-1.2.1.exe";

    // --- ParseSha256 ---

    [Fact]
    public void ParseSha256_RawHex_ReturnsUpperCase() =>
        UpdateService.ParseSha256(EmptyHashLower).ShouldBe(EmptyHashUpper);

    [Fact]
    public void ParseSha256_CoreutilsFormat_ReturnsHash() =>
        UpdateService.ParseSha256($"{EmptyHashLower} *{InstallerName}").ShouldBe(EmptyHashUpper);

    [Fact]
    public void ParseSha256_DoubleSpaceFormat_ReturnsHash() =>
        UpdateService.ParseSha256($"{EmptyHashLower}  {InstallerName}").ShouldBe(EmptyHashUpper);

    [Fact]
    public void ParseSha256_UsesFirstNonEmptyLine() =>
        UpdateService
            .ParseSha256($"\n  {EmptyHashLower} *{InstallerName}\nignored second line")
            .ShouldBe(EmptyHashUpper);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc123")] // too short
    public void ParseSha256_Malformed_ReturnsNull(string contents) =>
        UpdateService.ParseSha256(contents).ShouldBeNull();

    [Fact]
    public void ParseSha256_Null_ReturnsNull() => UpdateService.ParseSha256(null).ShouldBeNull();

    [Fact]
    public void ParseSha256_NonHex64Chars_ReturnsNull() =>
        UpdateService.ParseSha256(new string('g', 64)).ShouldBeNull();

    [Fact]
    public void ParseSha256_63HexChars_ReturnsNull() => UpdateService.ParseSha256(EmptyHashLower[1..]).ShouldBeNull();

    // --- VerifyFileHash ---

    [Fact]
    public void VerifyFileHash_CorrectHashLowerCase_ReturnsTrue() =>
        UpdateService.VerifyFileHash(AbcBytes, AbcHashLower).ShouldBeTrue();

    [Fact]
    public void VerifyFileHash_CorrectHashUpperCase_ReturnsTrue() =>
        UpdateService.VerifyFileHash(AbcBytes, AbcHashLower.ToUpperInvariant()).ShouldBeTrue();

    [Fact]
    public void VerifyFileHash_LeadingAsteriskTolerated_ReturnsTrue() =>
        UpdateService.VerifyFileHash(AbcBytes, "*" + AbcHashLower).ShouldBeTrue();

    [Fact]
    public void VerifyFileHash_EmptyFileMatchesEmptyVector_ReturnsTrue() =>
        UpdateService.VerifyFileHash([], EmptyHashLower).ShouldBeTrue();

    [Fact]
    public void VerifyFileHash_WrongHash_ReturnsFalse() =>
        UpdateService.VerifyFileHash(AbcBytes, new string('0', 64)).ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")] // not 64 hex chars
    public void VerifyFileHash_InvalidExpected_ReturnsFalse(string? expected) =>
        UpdateService.VerifyFileHash(AbcBytes, expected).ShouldBeFalse();

    // --- SelectInstallerAssets ---

    private static (string, string) Asset(string name) => (name, $"https://example/{name}");

    [Fact]
    public void SelectInstallerAssets_InstallerAndCompanionChecksum_SelectsBoth()
    {
        var result = UpdateService.SelectInstallerAssets([Asset(InstallerName), Asset(InstallerName + ".sha256")]);

        result.InstallerName.ShouldBe(InstallerName);
        result.InstallerUrl.ShouldBe($"https://example/{InstallerName}");
        result.Sha256Url.ShouldBe($"https://example/{InstallerName}.sha256");
    }

    [Fact]
    public void SelectInstallerAssets_InstallerOnly_ChecksumNull()
    {
        var result = UpdateService.SelectInstallerAssets([Asset(InstallerName)]);

        result.InstallerName.ShouldBe(InstallerName);
        result.Sha256Url.ShouldBeNull();
    }

    [Fact]
    public void SelectInstallerAssets_ChecksumWithoutInstaller_AllNull()
    {
        var result = UpdateService.SelectInstallerAssets([Asset(InstallerName + ".sha256")]);

        result.InstallerUrl.ShouldBeNull();
        result.InstallerName.ShouldBeNull();
        result.Sha256Url.ShouldBeNull();
    }

    [Fact]
    public void SelectInstallerAssets_NoExactCompanion_FallsBackToAnySha256()
    {
        var result = UpdateService.SelectInstallerAssets(
            [Asset(InstallerName), Asset("source.zip"), Asset("checksums.sha256")]
        );

        result.InstallerName.ShouldBe(InstallerName);
        result.Sha256Url.ShouldBe("https://example/checksums.sha256");
    }

    [Fact]
    public void SelectInstallerAssets_MatchesCaseInsensitively()
    {
        var result = UpdateService.SelectInstallerAssets([Asset("keypulse-signal-setup-1.2.1.EXE")]);

        result.InstallerName.ShouldBe("keypulse-signal-setup-1.2.1.EXE");
    }

    [Fact]
    public void SelectInstallerAssets_EmptyList_AllNull()
    {
        var result = UpdateService.SelectInstallerAssets([]);

        result.InstallerUrl.ShouldBeNull();
        result.InstallerName.ShouldBeNull();
        result.Sha256Url.ShouldBeNull();
    }

    // --- IsNewerVersion ---

    [Theory]
    [InlineData("1.2.0", "1.2.1", true)]
    [InlineData("1.2.0", "1.3.0", true)]
    [InlineData("1.2.0", "2.0.0", true)]
    [InlineData("1.2", "1.2.1", true)] // shorter current padded with zeros
    [InlineData("1.2.0", "1.2.0", false)]
    [InlineData("1.2.1", "1.2.0", false)]
    [InlineData("1.2.0", "1.2", false)] // shorter latest padded with zeros
    public void IsNewerVersion_ComparesNumerically(string current, string latest, bool expected) =>
        UpdateService.IsNewerVersion(current, latest).ShouldBe(expected);
}
