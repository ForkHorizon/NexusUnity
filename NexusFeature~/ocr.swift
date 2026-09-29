import Vision
import AppKit
import Foundation

func ocr(_ path: String) {
    guard let img = NSImage(contentsOfFile: path),
          let cg = img.cgImage(forProposedRect: nil, context: nil, hints: nil) else {
        print("=== \(path): FAILED TO LOAD")
        return
    }
    let request = VNRecognizeTextRequest()
    request.recognitionLevel = .accurate
    request.usesLanguageCorrection = false
    request.recognitionLanguages = ["en-US", "ru-RU"]
    let handler = VNImageRequestHandler(cgImage: cg, options: [:])
    do {
        try handler.perform([request])
        guard let obs = request.results as? [VNRecognizedTextObservation] else { return }
        // sort top-to-bottom (Vision bbox origin is bottom-left)
        let sorted = obs.sorted { $0.boundingBox.origin.y > $1.boundingBox.origin.y }
        print("=== \((path as NSString).lastPathComponent)")
        for o in sorted {
            if let t = o.topCandidates(1).first?.string {
                print(t)
            }
        }
    } catch {
        print("=== \(path): OCR ERROR \(error)")
    }
}

for arg in CommandLine.arguments.dropFirst() {
    ocr(arg)
}
