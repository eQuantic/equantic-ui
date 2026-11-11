import AVFoundation
import AppKit

// sheet <video> <output.png> <columns> <cell width> <time>...: a contact sheet of the video's frames at those times.
let arguments = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: arguments[1]))
let columns = Int(arguments[3])!, cellWidth = CGFloat(Double(arguments[4])!)
let times = arguments[5...].map { Double($0)! }
let generator = AVAssetImageGenerator(asset: asset)
generator.appliesPreferredTrackTransform = true
generator.requestedTimeToleranceBefore = .zero
generator.requestedTimeToleranceAfter = .zero
var frames: [CGImage] = []
for time in times {
    if let image = try? generator.copyCGImage(at: CMTime(seconds: time, preferredTimescale: 600), actualTime: nil) { frames.append(image) }
}
guard let first = frames.first else { print("no frames"); exit(1) }
let cellHeight = cellWidth * CGFloat(first.height) / CGFloat(first.width)
let rows = Int(ceil(Double(frames.count) / Double(columns)))
let gap: CGFloat = 6
let width = Int(CGFloat(columns) * cellWidth + CGFloat(columns + 1) * gap)
let height = Int(CGFloat(rows) * cellHeight + CGFloat(rows + 1) * gap)
let context = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
context.setFillColor(CGColor(red: 0.5, green: 0.5, blue: 0.5, alpha: 1))
context.fill(CGRect(x: 0, y: 0, width: width, height: height))
context.interpolationQuality = .high
for (index, frame) in frames.enumerated() {
    let column = index % columns, row = index / columns
    let x = gap + CGFloat(column) * (cellWidth + gap)
    let y = CGFloat(height) - gap - CGFloat(row + 1) * cellHeight - CGFloat(row) * gap
    context.draw(frame, in: CGRect(x: x, y: y, width: cellWidth, height: cellHeight))
}
guard let sheet = context.makeImage(),
      let png = NSBitmapImageRep(cgImage: sheet).representation(using: .png, properties: [:]) else {
    fputs("error: the contact sheet could not be drawn as a PNG\n", stderr)
    exit(1)
}
do {
    try png.write(to: URL(fileURLWithPath: arguments[2]))
} catch {
    fputs("error: \(arguments[2]) could not be written: \(error)\n", stderr)
    exit(1)
}
print("\(frames.count) frames \(width)x\(height)")
