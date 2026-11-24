using Xunit;

// BE-151: self-hosted API fixture использует process-wide env и одну одноразовую БД на прогон.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
