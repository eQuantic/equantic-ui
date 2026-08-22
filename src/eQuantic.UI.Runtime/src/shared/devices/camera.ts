/**
 * The browser's ICamera — getUserMedia, whose prompt IS the permission flow.
 *
 * The session's pixels never cross into script: the stream attaches to the CameraPreview node's
 * own <video> element, found by the session id the node carries. Capture draws the video into a
 * canvas and hands back a PNG — the browser's own encoder, no codec of ours.
 */

export interface CameraSessionValue {
  id: string;
  dispose(): void;
  capture(): unknown | null;
}

const streams = new Map<string, MediaStream>();

/** The stream a mounted <video data-eq-camera=…> should play, or undefined when it ended. */
export function cameraStream(id: string): MediaStream | undefined {
  return streams.get(id);
}

/** Wires every mounted preview to its stream — called after each lowering pass commits. */
export function attachCameraStreams(): void {
  if (typeof document === 'undefined') return;
  const mounted = document.querySelectorAll<HTMLVideoElement>('video[data-eq-camera]');
  for (const video of mounted) {
    const stream = streams.get(video.dataset.eqCamera ?? '');
    if (stream && video.srcObject !== stream) {
      video.srcObject = stream;
      void video.play().catch(() => {
        /* autoplay policies; muted video is allowed everywhere */
      });
    }
  }
}

export class WebCamera {
  private state: 'notDetermined' | 'granted' | 'denied' = 'notDetermined';

  get isAvailable(): boolean {
    return typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia;
  }

  get permission(): 'notDetermined' | 'granted' | 'denied' {
    return this.state;
  }

  async startPreviewAsync(): Promise<CameraSessionValue | null> {
    if (!this.isAvailable) return null;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ video: true });
      this.state = 'granted';
      const id = `cam-${Math.random().toString(36).slice(2)}`;
      streams.set(id, stream);
      attachCameraStreams();
      return {
        id,
        dispose: () => {
          streams.delete(id);
          for (const track of stream.getTracks()) track.stop();
        },
        capture: () => capture(id),
      };
    } catch {
      this.state = 'denied';
      return null;
    }
  }
}

function capture(id: string): unknown | null {
  if (typeof document === 'undefined') return null;
  const video = document.querySelector<HTMLVideoElement>(`video[data-eq-camera="${id}"]`);
  if (!video || video.videoWidth === 0) return null;

  const canvas = document.createElement('canvas');
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  canvas.getContext('2d')?.drawImage(video, 0, 0);
  const dataUri = canvas.toDataURL('image/png');
  const bytes = Uint8Array.from(atob(dataUri.split(',')[1]), (c) => c.charCodeAt(0));
  // The same shape ImageData crosses with everywhere else (see photo-library).
  return {
    bytes,
    mimeType: 'image/png',
    width: canvas.width,
    height: canvas.height,
    name: null,
  };
}
