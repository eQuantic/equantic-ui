# image-framing Specification

## Purpose
How an image places and scales its picture inside its box: the point of the picture its box holds,
and how much closer than its fit the picture is drawn, the same on the web and on Photon.

## Requirements

### Requirement: An image holds its picture by a focal point

`Image.FocalPoint` SHALL be a point of fractions of the picture (0 to 1 on each axis, the centre by
default), and the picture's point at that fraction SHALL stand at the same fraction of the image's box,
whatever the fit. A focal point outside 0 to 1 on either axis SHALL throw `ArgumentOutOfRangeException`
where it is set. The web SHALL write it as `object-position` in percentages, on the server and after
hydration alike, and write nothing for the centre; Photon SHALL place the picture by the same rule.

#### Scenario: A portrait covered in a round frame

- **WHEN** a 220 by 220 `Image` with `ImageFit.Cover`, a corner radius of 110 and a focal point of
  (0.3, 0.7) is drawn
- **THEN** the web writes `object-position: 30% 70%`, and the C# and the runtime's lowering write the
  same atomic classes

#### Scenario: The default

- **WHEN** an `Image` sets no focal point
- **THEN** the web writes no `object-position`, and the markup is what it was before

### Requirement: An image zooms around its focal point

`Image.Zoom` SHALL draw the picture that many times its fit (1 by default), scaled around the focal
point's place in the box, so that point keeps its place and a picture that covered its box still covers
it. A zoom below 1 or not finite SHALL throw `ArgumentOutOfRangeException` where it is set. The web SHALL
write a `scale` around a `transform-origin` at the focal point, inside a frame of the box's size that
clips it and carries the corner radius; Photon SHALL scale its destination around the same point and
clip to the box.

#### Scenario: A zoomed portrait

- **WHEN** the portrait above is drawn with a zoom of 1.5
- **THEN** the web writes `transform: scale(1.5)` with `transform-origin: 30% 70%` on the picture, inside a
  220 by 220 frame with `overflow: hidden` and the radius, and Photon draws the picture 1.5 times larger
  around the same point, clipped to the box
