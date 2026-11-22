- **2026-11-22 · The Conformance host resolves each reference once, and fits a CI runner**: the
  ubuntu Conformance job died mid-suite with every test green. Its log said "The runner has received a
  shutdown signal", the way a GitHub runner goes when it runs out of memory. The job hung for 40
  minutes on the release's pull request. Measured locally:
  - the suite's host reached a 22 GB working set over a 1.2 GB managed heap, and capping the heap
    changed nothing;
  - the memory fell only when a gen 2 collection let finalizers run.

  Each case's .NET side is a script, and scripting's own resolver read the framework assemblies a
  compilation finds missing again for every script, into native buffers the GC never saw. The
  evaluator now resolves through `ReferencesResolvedOnce`, which answers each reference once per
  process. The whole suite (8,424 cases) runs in one host with a 2.8 GB peak, in 6 minutes where its
  six shards took about 17. `ScriptReferencesTests` holds the resolver to one read per assembly, and
  fails with scripting's own resolver, which read `System.Security.Claims` three times for three
  scripts ([#1816](https://github.com/eQuantic/equantic-ui/issues/1816)).
