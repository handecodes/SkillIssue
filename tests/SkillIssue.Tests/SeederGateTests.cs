using SeederGen;

namespace SkillIssue.Tests;

public class SeederGateTests
{
    // Proves the gate's own logic: it reports IDENTICAL for the real in-sync seeder, and DIFFERS
    // (non-zero exit) the moment a row drifts from the manifest. This is what guards a broken
    // DbSeeder from shipping — exercised in-process here, not only by the CI workflow.
    [Fact]
    public void Verify_PassesOnRealSeeder_AndDetectsDriftAsDiffers()
    {
        var root = RepoRoot();
        var manifestPath = Path.Combine(root, "tools", "SeederGen", "manifest.json");
        var seederPath = Path.Combine(root, "src", "SkillIssue.Data", "Seeding", "DbSeeder.cs");
        var manifest = Program.Load(manifestPath);

        var temp = Path.Combine(Path.GetTempPath(), "seedergate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            // Control: the real, in-sync seeder must verify clean (0). Also proves the harness/paths.
            var passExit = Program.Verify(manifest, seederPath, Path.Combine(temp, "real"));
            Assert.Equal(0, passExit);

            // Drift one row away from the manifest, then re-verify against the SAME manifest.
            var drifted = File.ReadAllText(seederPath).Replace("Humanizr/Humanizer", "Humanizr/HumanizerX");
            Assert.Contains("Humanizr/HumanizerX", drifted); // guard: the drift actually applied
            var driftedSeeder = Path.Combine(temp, "DbSeeder.cs");
            File.WriteAllText(driftedSeeder, drifted);

            var driftExit = Program.Verify(manifest, driftedSeeder, Path.Combine(temp, "drift"));
            Assert.NotEqual(0, driftExit); // gate catches the drifted row as DIFFERS
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "SeederGen", "manifest.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
