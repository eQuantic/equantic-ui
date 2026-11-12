import AVFoundation
import CoreGraphics
import Foundation

// changes <video> [step] [threshold]: the moments the picture changes, and by how much (0-255 mean difference).
let arguments = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: arguments[1]))
let step = arguments.count > 2 ? Double(arguments[2])! : 0.1
let threshold = arguments.count > 3 ? Double(arguments[3])! : 1.0
let from = arguments.count > 4 ? Double(arguments[4])! : 0.0
let to = arguments.count > 5 ? Double(arguments[5])! : Double.infinity
let done = DispatchSemaphore(value: 0)
Task {
    let duration = CMTimeGetSeconds(try await asset.load(.duration))
    let generator = AVAssetImageGenerator(asset: asset)
    generator.appliesPreferredTrackTransform = true
    generator.requestedTimeToleranceBefore = .zero
    generator.requestedTimeToleranceAfter = .zero
    generator.maximumSize = CGSize(width: 96, height: 96)
    let side = 48
    var previous: [UInt8]? = nil
    var time = from
    while time < min(duration, to) {
        if let image = try? generator.copyCGImage(at: CMTime(seconds: time, preferredTimescale: 600), actualTime: nil) {
            var pixels = [UInt8](repeating: 0, count: side * side)
            let context = CGContext(data: &pixels, width: side, height: side, bitsPerComponent: 8, bytesPerRow: side,
                                    space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue)!
            context.draw(image, in: CGRect(x: 0, y: 0, width: side, height: side))
            if let before = previous {
                var total = 0
                for index in 0..<pixels.count { total += abs(Int(pixels[index]) - Int(before[index])) }
                let mean = Double(total) / Double(pixels.count)
                if mean >= threshold { print(String(format: "%.2f %.1f", time, mean)) }
            }
            previous = pixels
        }
        time += step
    }
    done.signal()
}
done.wait()
