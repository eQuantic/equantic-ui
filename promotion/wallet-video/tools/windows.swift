import CoreGraphics
import Foundation

// windows <owner substring>: the on-screen windows of the processes whose name holds it.
let wanted = CommandLine.arguments.count > 1 ? CommandLine.arguments[1].lowercased() : ""
let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as! [[String: Any]]
for window in list {
    let owner = (window[kCGWindowOwnerName as String] as? String) ?? ""
    guard wanted.isEmpty || owner.lowercased().contains(wanted) else { continue }
    let bounds = window[kCGWindowBounds as String] as! [String: CGFloat]
    let layer = window[kCGWindowLayer as String] as? Int ?? -1
    print("\(window[kCGWindowNumber as String] ?? 0) owner=\(owner) pid=\(window[kCGWindowOwnerPID as String] ?? 0) layer=\(layer) x=\(Int(bounds["X"]!)) y=\(Int(bounds["Y"]!)) w=\(Int(bounds["Width"]!)) h=\(Int(bounds["Height"]!)) name=\(window[kCGWindowName as String] ?? "")")
}
