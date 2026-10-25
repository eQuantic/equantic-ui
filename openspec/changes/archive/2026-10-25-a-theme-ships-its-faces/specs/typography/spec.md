# Spec Delta

## ADDED Requirements

### Requirement: A style names its face by a family that carries its files

`TypeStyle.Family`, `IAppTheme.TextFamily` and `IAppTheme.MonoFamily` SHALL be a `FontFamily`, so a style that names a face a theme ships carries the face's files with it. The measurer, the rasterizer and the raster cache SHALL key on the family as they key on the rest of the style.

#### Scenario: A role cut in a face the theme ships

- **WHEN** a theme cuts its Heading role in a family it ships, on a machine where that family is not installed
- **THEN** Photon measures and draws a heading in that family, and the web's heading class names it and the page declares its files
