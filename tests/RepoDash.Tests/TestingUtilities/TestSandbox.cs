using System.Security.Cryptography;
using System.Text;

namespace RepoDash.Tests.TestingUtilities
{
    /// <summary>
    /// Creates a deterministic, large microservices-style repository tree for tests.
    /// Every repo has a ".git" folder with fake but realistic data:
    ///   - .git/HEAD -> "ref: refs/heads/{branch}"
    ///   - .git/refs/heads/{branch} -> 40-hex fake commit (SHA1 of repo path)
    ///
    /// Branch distribution (cyclic, deterministic per creation order):
    ///   master (majority), feature/RD-42_ImportantFeature, bug/RD-666_bad_bug_to_fix
    ///   Pattern of 10: [M,M,M,M,M,M,M,F,F,B]
    ///
    /// Also creates one top-level folder that is NOT a repo (.git absent) for negative tests.
    ///
    /// Parallel-safe:
    ///   Root = %TEMP%\RepoDash_Sandboxes\{RunId}\{InstanceGuid}
    ///
    /// Preservation:
    ///   - pass preserve: true, or set REPO_DASH_PRESERVE_SANDBOX=true
    /// </summary>
    public sealed class TestSandbox : IDisposable
    {
        public string Root { get; }
        public bool Preserve { get; }

        private static readonly string BaseRoot =
            Path.Combine(Path.GetTempPath(), "RepoDash_Sandboxes");

        private static readonly string[] BranchCycle =
        {
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchMaster,
            SandboxLayout.BranchFeature,
            SandboxLayout.BranchFeature,
            SandboxLayout.BranchBugfix
        };
        private int _branchIndex = 0;

        public TestSandbox(bool preserve = false, string? name = null)
        {
            Preserve = preserve || IsPreserveEnvSet();

            var runId = string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : Sanitize(name!);
            var instanceId = Guid.NewGuid().ToString("N");

            var runRoot = Path.Combine(BaseRoot, runId);
            Directory.CreateDirectory(runRoot);

            Root = Path.Combine(runRoot, instanceId);
            Directory.CreateDirectory(Root);
        }

        public void Dispose()
        {
            if (Preserve) return;

            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, recursive: true);

                var parent = Directory.GetParent(Root)?.FullName;
                if (!string.IsNullOrEmpty(parent) &&
                    Directory.Exists(parent) &&
                    !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent, recursive: true);
                }
            }
            catch { /* best-effort cleanup */ }
        }

        // ----- Public API -----------------------------------------------------

        public SandboxLayout CreateLargeSystem()
        {
            var layout = new SandboxLayout(Root);

            // Non-repo container folder (no .git) — must NOT be discovered
            layout.NonRepoContainerPath = EnsureDir("container");

            // apps (10)
            layout.Apps = new[]
            {
                MakeCSharpRepo(layout, "apps/web/WebPortal", "WebPortal.sln"),
                MakeCSharpRepo(layout, "apps/web/AdminPortal", "AdminPortal.sln"),
                MakeCSharpRepo(layout, "apps/mobile/MobileApp", "MobileApp.sln"),
                MakeCSharpRepo(layout, "apps/kiosk/KioskApp", "KioskApp.sln"),
                MakeCSharpRepo(layout, "apps/backoffice/BackOfficeApp", "BackOfficeApp.sln"),
                MakeCSharpRepo(layout, "apps/partner/PartnerPortal", "PartnerPortal.sln"),
                MakeCSharpRepo(layout, "apps/support/SupportDeskApp", "SupportDeskApp.sln"),
                MakeCSharpRepo(layout, "apps/reporting/ReportingPortal", "ReportingPortal.sln"),
                MakeCSharpRepo(layout, "apps/ops/OpsConsole", "OpsConsole.sln"),
                MakeCSharpRepo(layout, "apps/developer/DeveloperPortal", "DeveloperPortal.sln"),
            };

            // services (12)
            layout.Services = new[]
            {
                MakeCSharpRepo(layout, "services/orders/OrderService", "OrderService.sln"),
                MakeCSharpRepo(layout, "services/payments/PaymentService", "PaymentService.sln"),
                MakeCSharpRepo(layout, "services/catalog/CatalogService", "CatalogService.sln"),
                MakeCSharpRepo(layout, "services/inventory/InventoryService", "InventoryService.sln"),
                MakeCSharpRepo(layout, "services/shipping/ShippingService", "ShippingService.sln"),
                MakeCSharpRepo(layout, "services/pricing/PricingService", "PricingService.sln"),
                MakeCSharpRepo(layout, "services/notifications/NotificationService", "NotificationService.sln"),
                MakeCSharpRepo(layout, "services/recommendations/RecommendationService", "RecommendationService.sln"),
                MakeCSharpRepo(layout, "services/search/SearchService", "SearchService.sln"),
                MakeCSharpRepo(layout, "services/sessions/SessionService", "SessionService.sln"),
                MakeCSharpRepo(layout, "services/users/UserProfileService", "UserProfileService.sln"),
                MakeCSharpRepo(layout, "services/audit/AuditService", "AuditService.sln"),
            };

            // components (10)
            layout.Components = new[]
            {
                MakeCSharpRepo(layout, "components/auth/CommonAuth", "CommonAuth.sln"),
                MakeCSharpRepo(layout, "components/messaging/CoreMessaging", "CoreMessaging.sln"),
                MakeCSharpRepo(layout, "components/messaging/EventBusAdapter", "EventBusAdapter.sln"),
                MakeCSharpRepo(layout, "components/storage/StorageAbstractions", "StorageAbstractions.sln"),
                MakeCSharpRepo(layout, "components/caching/Caching", "Caching.sln"),
                MakeCSharpRepo(layout, "components/localization/Localization", "Localization.sln"),
                MakeCSharpRepo(layout, "components/feature-flags/FeatureFlags", "FeatureFlags.sln"),
                MakeCSharpRepo(layout, "components/tracing/Tracing", "Tracing.sln"),
                MakeCSharpRepo(layout, "components/resilience/Resilience", "Resilience.sln"),
                MakeCSharpRepo(layout, "components/background-jobs/BackgroundJobs", "BackgroundJobs.sln"),
            };

            // sql (10) – no .sln
            layout.Sql = new[]
            {
                MakeSqlRepo(layout, "sql/orders-db"),
                MakeSqlRepo(layout, "sql/payments-db"),
                MakeSqlRepo(layout, "sql/catalog-db"),
                MakeSqlRepo(layout, "sql/inventory-db"),
                MakeSqlRepo(layout, "sql/shipping-db"),
                MakeSqlRepo(layout, "sql/identity-db"),
                MakeSqlRepo(layout, "sql/reporting-db"),
                MakeSqlRepo(layout, "sql/analytics-db"),
                MakeSqlRepo(layout, "sql/audit-db"),
                MakeSqlRepo(layout, "sql/events-db"),
            };

            // utilities (10)
            layout.Utilities = new[]
            {
                MakeCSharpRepo(layout, "utilities/build/BuildTools", "BuildTools.sln"),
                MakeCSharpRepo(layout, "utilities/codegen/CodeGen", "CodeGen.sln"),
                MakeCSharpRepo(layout, "utilities/db/DbMigrationsTool", "DbMigrationsTool.sln"),
                MakeCSharpRepo(layout, "utilities/perf/LoadTester", "LoadTester.sln"),
                MakeCSharpRepo(layout, "utilities/metrics/MetricsCollector", "MetricsCollector.sln"),
                MakeCSharpRepo(layout, "utilities/logging/LogShipper", "LogShipper.sln"),
                MakeCSharpRepo(layout, "utilities/security/SecretRotator", "SecretRotator.sln"),
                MakeCSharpRepo(layout, "utilities/config/ConfigSync", "ConfigSync.sln"),
                MakeCSharpRepo(layout, "utilities/gitops/GitOps", "GitOps.sln"),
                MakeCSharpRepo(layout, "utilities/images/ImageBuilder", "ImageBuilder.sln"),
            };

            // contracts (10)
            layout.Contracts = new[]
            {
                MakeCSharpRepo(layout, "contracts/public/public-webapi", "PublicWebApi.sln"),
                MakeCSharpRepo(layout, "contracts/partner/partner-webapi", "PartnerWebApi.sln"),
                MakeCSharpRepo(layout, "contracts/internal/internal-events", "InternalEvents.sln"),
                MakeCSharpRepo(layout, "contracts/payments/payments-events", "PaymentsEvents.sln"),
                MakeCSharpRepo(layout, "contracts/search/search-api", "SearchApi.sln"),
                MakeCSharpRepo(layout, "contracts/reporting/reporting-api", "ReportingApi.sln"),
                MakeCSharpRepo(layout, "contracts/identity/identity-api", "IdentityApi.sln"),
                MakeCSharpRepo(layout, "contracts/shipping/shipping-api", "ShippingApi.sln"),
                MakeCSharpRepo(layout, "contracts/inventory/inventory-api", "InventoryApi.sln"),
                MakeCSharpRepo(layout, "contracts/users/user-profile-api", "UserProfileApi.sln"),
            };

            // integration-tests (10)
            layout.IntegrationTests = new[]
            {
                MakeCSharpRepo(layout, "integration-tests/orders/OrdersE2E", "OrdersE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/payments/PaymentsE2E", "PaymentsE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/catalog/CatalogE2E", "CatalogE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/inventory/InventoryE2E", "InventoryE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/shipping/ShippingE2E", "ShippingE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/search/SearchE2E", "SearchE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/identity/IdentityE2E", "IdentityE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/apps/WebPortalE2E", "WebPortalE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/apps/PartnerPortalE2E", "PartnerPortalE2E.sln"),
                MakeCSharpRepo(layout, "integration-tests/full/RegressionsSuite", "RegressionsSuite.sln"),
            };

            // Aggregate
            layout.AllRepos = layout
                .AllCategories()
                .SelectMany(cat => cat)
                .ToArray();

            return layout;
        }

        // ----- Internals ------------------------------------------------------

        private string MakeCSharpRepo(SandboxLayout layout, string relativePath, string slnName)
        {
            var branch = NextBranch();
            var repoRoot = EnsureDir(relativePath);
            CreateFakeGit(repoRoot, branch);
            var slnPath = Path.Combine(repoRoot, slnName);
            if (!File.Exists(slnPath)) File.WriteAllText(slnPath, string.Empty);

            layout.BranchByRepoPath[repoRoot] = branch;
            AddToBranchList(layout, repoRoot, branch);
            return repoRoot;
        }

        private string MakeSqlRepo(SandboxLayout layout, string relativePath)
        {
            var branch = NextBranch();
            var repoRoot = EnsureDir(relativePath);
            CreateFakeGit(repoRoot, branch);

            var schema = EnsureDir(Path.Combine(relativePath, "schema"));
            File.WriteAllText(Path.Combine(schema, "001_init.sql"), "-- init");
            File.WriteAllText(Path.Combine(schema, "002_indexes.sql"), "-- indexes");

            layout.BranchByRepoPath[repoRoot] = branch;
            AddToBranchList(layout, repoRoot, branch);
            return repoRoot;
        }

        private void CreateFakeGit(string repoRoot, string branch)
        {
            var git = Path.Combine(repoRoot, ".git");
            Directory.CreateDirectory(git);

            // HEAD
            File.WriteAllText(Path.Combine(git, "HEAD"), $"ref: refs/heads/{branch}\n");

            // refs/heads/<branch>
            var refsHeads = Path.Combine(git, "refs", "heads");
            Directory.CreateDirectory(refsHeads);

            // If branch has slashes, ensure nested dirs (e.g., feature/RD-42_ImportantFeature)
            var branchRefPath = Path.Combine(git, "refs", "heads", branch.Replace('/', Path.DirectorySeparatorChar));
            var branchDir = Path.GetDirectoryName(branchRefPath)!;
            Directory.CreateDirectory(branchDir);

            File.WriteAllText(branchRefPath, FakeShaFor(repoRoot) + "\n");
        }

        private static string FakeShaFor(string input)
        {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            // Ensure 40 hex chars (SHA1) — already satisfied
            return sb.ToString();
        }

        private string NextBranch()
        {
            var branch = BranchCycle[_branchIndex % BranchCycle.Length];
            _branchIndex++;
            return branch;
        }

        private static void AddToBranchList(SandboxLayout layout, string repoRoot, string branch)
        {
            if (string.Equals(branch, SandboxLayout.BranchMaster, StringComparison.OrdinalIgnoreCase))
                layout.OnMaster.Add(repoRoot);
            else if (string.Equals(branch, SandboxLayout.BranchFeature, StringComparison.OrdinalIgnoreCase))
                layout.OnFeature.Add(repoRoot);
            else if (string.Equals(branch, SandboxLayout.BranchBugfix, StringComparison.OrdinalIgnoreCase))
                layout.OnBugfix.Add(repoRoot);
        }

        private string EnsureDir(string relativePath)
        {
            var full = Path.Combine(Root, Normalize(relativePath));
            Directory.CreateDirectory(full);
            return full;
        }

        private static string Normalize(string rp) => rp.Replace('/', Path.DirectorySeparatorChar);

        private static bool IsPreserveEnvSet()
        {
            var v = Environment.GetEnvironmentVariable("REPO_DASH_PRESERVE_SANDBOX");
            return !string.IsNullOrWhiteSpace(v) &&
                   v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }

    /// <summary>
    /// Strongly-typed description of everything the sandbox created,
    /// including the current branch for each repo.
    /// </summary>
    public sealed class SandboxLayout
    {
        // Branch names (public constants for use in tests)
        public const string BranchMaster = "master";
        public const string BranchFeature = "feature/RD-42_ImportantFeature";
        public const string BranchBugfix = "bug/RD-666_bad_bug_to_fix";

        public string Root { get; }
        public string NonRepoContainerPath { get; set; } = string.Empty;

        public IReadOnlyList<string> Apps { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Services { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Components { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Sql { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Utilities { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Contracts { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> IntegrationTests { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> AllRepos { get; set; } = Array.Empty<string>();

        /// <summary>RepoPath -> BranchName</summary>
        public Dictionary<string, string> BranchByRepoPath { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Convenience lists per branch (RepoPaths).</summary>
        public List<string> OnMaster { get; } = new();
        public List<string> OnFeature { get; } = new();
        public List<string> OnBugfix { get; } = new();

        public int TotalRepoCount => AllRepos.Count;

        public SandboxLayout(string root) => Root = root;

        public IEnumerable<IReadOnlyList<string>> AllCategories()
        {
            yield return Apps; yield return Services; yield return Components;
            yield return Sql; yield return Utilities; yield return Contracts; yield return IntegrationTests;
        }
    }
}
