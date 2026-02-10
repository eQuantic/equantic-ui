namespace eQuantic.UI.Images.Tests;

/// <summary>
/// xUnit collection to prevent parallel execution of tests that share
/// the mutable static ImageOptimizationState.
/// </summary>
[CollectionDefinition("ImageState")]
public class ImageStateCollection;
