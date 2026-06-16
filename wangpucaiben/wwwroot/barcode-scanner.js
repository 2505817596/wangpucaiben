window.barcodeScanner = (() => {
    let stream = null;
    let timerId = null;
    let lastBarcode = "";
    let lastDetectedAt = 0;

    async function start(videoElementId, dotNetRef) {
        stop();
        lastBarcode = "";
        lastDetectedAt = 0;

        const videoElement = document.getElementById(videoElementId);
        if (!videoElement || typeof videoElement.play !== "function") {
            throw new Error("未找到可用的视频预览元素");
        }

        if (!("mediaDevices" in navigator) || !navigator.mediaDevices.getUserMedia) {
            throw new Error("当前浏览器不支持相机调用");
        }

        if (!("BarcodeDetector" in window)) {
            throw new Error("当前浏览器不支持条码识别，请使用新版 Chrome 或 Edge");
        }

        const detector = new BarcodeDetector({
            formats: [
                "ean_13",
                "ean_8",
                "upc_a",
                "upc_e",
                "code_128",
                "code_39",
                "itf",
                "qr_code"
            ]
        });

        stream = await navigator.mediaDevices.getUserMedia({
            video: {
                facingMode: { ideal: "environment" }
            },
            audio: false
        });

        videoElement.srcObject = stream;
        await videoElement.play();

        timerId = window.setInterval(async () => {
            if (!videoElement.videoWidth || !videoElement.videoHeight) {
                return;
            }

            try {
                const barcodes = await detector.detect(videoElement);
                const first = barcodes.find(x => x.rawValue);
                if (!first) {
                    return;
                }

                const rawValue = first.rawValue.trim();
                const now = Date.now();
                if (!rawValue) {
                    return;
                }

                // Prevent the same barcode from firing repeatedly while it remains in frame.
                if (rawValue === lastBarcode && now - lastDetectedAt < 1500) {
                    return;
                }

                lastBarcode = rawValue;
                lastDetectedAt = now;

                await dotNetRef.invokeMethodAsync("OnBarcodeDetected", rawValue);
            }
            catch {
                // Ignore transient detection failures while camera is active.
            }
        }, 350);
    }

    function stop() {
        if (timerId) {
            window.clearInterval(timerId);
            timerId = null;
        }

        if (stream) {
            for (const track of stream.getTracks()) {
                track.stop();
            }

            stream = null;
        }
    }

    return { start, stop };
})();
