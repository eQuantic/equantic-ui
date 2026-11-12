import AppKit
import AVFoundation
import CoreMedia
import Foundation
import ScreenCaptureKit

// winrec <window id> <seconds> <output.mov>: one window, recorded by itself whatever covers it.
final class Recorder: NSObject, SCStreamOutput {
    let writer: AVAssetWriter
    let input: AVAssetWriterInput
    var started = false
    var frames = 0

    init(url: URL, width: Int, height: Int) throws {
        writer = try AVAssetWriter(outputURL: url, fileType: .mov)
        input = AVAssetWriterInput(mediaType: .video, outputSettings: [
            AVVideoCodecKey: AVVideoCodecType.h264,
            AVVideoWidthKey: width,
            AVVideoHeightKey: height,
            AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 16_000_000],
        ])
        input.expectsMediaDataInRealTime = true
        writer.add(input)
    }

    func stream(_ stream: SCStream, didOutputSampleBuffer sample: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .screen, sample.isValid,
              let attachments = CMSampleBufferGetSampleAttachmentsArray(sample, createIfNecessary: false) as? [[SCStreamFrameInfo: Any]],
              let status = attachments.first?[.status] as? Int, status == SCFrameStatus.complete.rawValue else { return }
        if !started {
            writer.startWriting()
            writer.startSession(atSourceTime: CMSampleBufferGetPresentationTimeStamp(sample))
            started = true
        }
        if input.isReadyForMoreMediaData, input.append(sample) { frames += 1 }
    }
}

// A command line tool has no connection to the window server until it asks for one.
_ = NSApplication.shared
let arguments = CommandLine.arguments
let windowID = CGWindowID(arguments[1])!
let seconds = Double(arguments[2])!
let output = URL(fileURLWithPath: arguments[3])
try? FileManager.default.removeItem(at: output)

let done = DispatchSemaphore(value: 0)
Task {
    do {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: false)
        guard let window = content.windows.first(where: { $0.windowID == windowID }) else {
            print("no window \(windowID)"); exit(2)
        }
        let scale = 2
        let width = Int(window.frame.width) * scale, height = Int(window.frame.height) * scale
        let configuration = SCStreamConfiguration()
        configuration.width = width
        configuration.height = height
        configuration.minimumFrameInterval = CMTime(value: 1, timescale: 60)
        configuration.showsCursor = false
        configuration.pixelFormat = kCVPixelFormatType_32BGRA
        let recorder = try Recorder(url: output, width: width, height: height)
        let stream = SCStream(filter: SCContentFilter(desktopIndependentWindow: window), configuration: configuration, delegate: nil)
        try stream.addStreamOutput(recorder, type: .screen, sampleHandlerQueue: DispatchQueue(label: "frames"))
        try await stream.startCapture()
        try await Task.sleep(nanoseconds: UInt64(seconds * 1_000_000_000))
        try await stream.stopCapture()
        recorder.input.markAsFinished()
        await recorder.writer.finishWriting()
        print("frames \(recorder.frames) size \(width)x\(height) status \(recorder.writer.status.rawValue)")
    } catch {
        print("error \(error)")
    }
    done.signal()
}
done.wait()
