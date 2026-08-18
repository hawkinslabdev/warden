// ContentService publishes ambient statics on rebuild, so test classes may not run against each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
