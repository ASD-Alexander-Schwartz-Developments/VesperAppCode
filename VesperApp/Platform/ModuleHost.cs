using System;
using System.Collections.Generic;
using System.Diagnostics;
using ASD.Contracts;
using VesperApp.Models;

namespace ASD.Modules
{
    /// <summary>
    /// A loadable feature module. Modules are how entitlement-gated, optionally
    /// proprietary functionality (cloud sync, GNSS post-processing, remote BLE
    /// download) plugs into the shell without the open-source core referencing any of
    /// it. The open-source build ships zero modules, so these contributions are empty
    /// today — but the seam is live. See docs/ARCHITECTURE.md.
    /// <para>
    /// NOTE: <see cref="GetNavCategories"/> returns the shell's <see cref="Category"/>
    /// type for now. When the open/proprietary split is done, this contract moves to
    /// the ASD.Contracts assembly and returns a UI-agnostic descriptor instead; the
    /// shell then maps that to <see cref="Category"/> at the boundary.
    /// </para>
    /// </summary>
    public interface IModule
    {
        /// <summary>Stable module id, e.g. "asd.cloudsync".</summary>
        string Id { get; }

        /// <summary>Human-readable name for diagnostics.</summary>
        string DisplayName { get; }

        /// <summary>The entitlement the account must hold for this module to load
        /// (see <see cref="Entitlements"/>). Empty string means "always available".</summary>
        string RequiredEntitlement { get; }

        /// <summary>Called once before the module contributes UI.</summary>
        void Initialize(IPlatformContext context);

        /// <summary>Navigation entries this module adds to the shell sidebar.</summary>
        IReadOnlyList<Category> GetNavCategories();
    }

    /// <summary>
    /// Discovers feature modules and exposes the nav categories the entitled ones
    /// contribute. In the open-source build no modules are registered, so
    /// <see cref="GetEntitledNavCategories"/> returns an empty list and the shell's
    /// sidebar is unchanged. Plugin discovery from a /plugins folder is intentionally
    /// left as a future step (documented), so this stub has no filesystem or assembly
    /// dependencies.
    /// </summary>
    public sealed class ModuleHost
    {
        /// <summary>Shell-wide module host. The desktop shell owns module navigation;
        /// the daemon does not use this (it has no UI).</summary>
        public static ModuleHost Instance { get; } = new ModuleHost();

        private readonly List<IModule> _modules = new();
        private IPlatformContext? _context;

        /// <summary>Bind the platform context modules receive at init.</summary>
        public void UseContext(IPlatformContext context) => _context = context;

        /// <summary>Register a module instance (future: called by the plugin loader).</summary>
        public void Register(IModule module)
        {
            if (_context != null)
                module.Initialize(_context);
            _modules.Add(module);
            Debug.WriteLine($"[ModuleHost] registered module '{module.Id}' ({module.DisplayName}).");
        }

        /// <summary>
        /// Nav categories contributed by modules the current account is entitled to.
        /// Empty in the open-source build. The shell appends these to its static
        /// sidebar entries.
        /// </summary>
        public IReadOnlyList<Category> GetEntitledNavCategories(IEntitlementProvider entitlements)
        {
            var result = new List<Category>();
            foreach (IModule module in _modules)
            {
                bool entitled = string.IsNullOrEmpty(module.RequiredEntitlement)
                                || entitlements.Has(module.RequiredEntitlement);
                if (!entitled)
                    continue;
                result.AddRange(module.GetNavCategories());
            }
            return result;
        }
    }
}
