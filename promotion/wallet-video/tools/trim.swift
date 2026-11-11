import AVFoundation
import Foundation

// trim <input> <start> <duration> <width> <height> <output.mp4>: a stretch of a recording, at a constant
// thirty frames a second and the size asked for, whatever rate the recording itself has.
let arguments = CommandLine.arguments
let asset = AVURLAsset(url: URL(fileURLWithPath: arguments[1]))
let start = Double(arguments[2])!, length = Double(arguments[3])!
let size = CGSize(width: Double(arguments[4])!, height: Double(arguments[5])!)
let output = URL(fileURLWithPath: arguments[6])
try? FileManager.default.removeItem(at: output)

let done = DispatchSemaphore(value: 0)
Task {
    do {
        let track = try await asset.loadTracks(withMediaType: .video).first!
        let natural = try await track.load(.naturalSize)
        let range = CMTimeRange(start: CMTime(seconds: start, preferredTimescale: 600),
                                duration: CMTime(seconds: length, preferredTimescale: 600))

        let composition = AVMutableComposition()
        let video = composition.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
        try video.insertTimeRange(range, of: track, at: .zero)

        let instruction = AVMutableVideoCompositionInstruction()
        instruction.timeRange = CMTimeRange(start: .zero, duration: range.duration)
        let layer = AVMutableVideoCompositionLayerInstruction(assetTrack: video)
        layer.setTransform(CGAffineTransform(scaleX: size.width / natural.width, y: size.height / natural.height), at: .zero)
        instruction.layerInstructions = [layer]

        let render = AVMutableVideoComposition()
        render.renderSize = size
        render.frameDuration = CMTime(value: 1, timescale: 30)
        render.instructions = [instruction]

        guard let export = AVAssetExportSession(asset: composition, presetName: AVAssetExportPresetHighestQuality) else {
            print("error: no export session for \(output.lastPathComponent) at the highest-quality preset")
            done.signal()
            return
        }
        export.videoComposition = render
        export.outputURL = output
        export.outputFileType = .mp4
        await export.export()
        print("\(output.lastPathComponent): status \(export.status.rawValue) \(export.error.map { "\($0)" } ?? "")")
    } catch {
        print("error \(error)")
    }
    done.signal()
}
done.wait()
