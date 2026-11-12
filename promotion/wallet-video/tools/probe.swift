import AVFoundation
import AppKit

// probe <video> [<seconds> <png>]...: the video's size, rate and length, and a frame at each time asked for.
let arguments = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: arguments[1]))
let semaphore = DispatchSemaphore(value: 0)
Task {
    let duration = try await asset.load(.duration)
    if let track = try await asset.loadTracks(withMediaType: .video).first {
        let size = try await track.load(.naturalSize)
        let rate = try await track.load(.nominalFrameRate)
        print("size \(Int(size.width))x\(Int(size.height)) fps \(rate) duration \(CMTimeGetSeconds(duration))")
    }
    let generator = AVAssetImageGenerator(asset: asset)
    generator.appliesPreferredTrackTransform = true
    generator.requestedTimeToleranceBefore = .zero
    generator.requestedTimeToleranceAfter = .zero
    var index = 2
    while index + 1 < arguments.count {
        let time = CMTime(seconds: Double(arguments[index])!, preferredTimescale: 600)
        let image = try generator.copyCGImage(at: time, actualTime: nil)
        let bitmap = NSBitmapImageRep(cgImage: image)
        try bitmap.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: arguments[index + 1]))
        index += 2
    }
    semaphore.signal()
}
semaphore.wait()
