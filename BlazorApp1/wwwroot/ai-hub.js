let worker;


function predict() {
    const video = document.querySelector("video");
    const canvas = document.querySelector("canvas");
    const context = canvas.getContext("2d");
    worker = new Worker("worker.js");

    let boxes = [];

    video.addEventListener("play", () => {
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;

        intervalId = setInterval(() => {
            context.drawImage(video, 0, 0);

            const date = new Date();
            const year = date.getFullYear();
            const month = ("0" + (date.getMonth() + 1)).slice(-2);
            const day = ("0" + date.getDate()).slice(-2);
            const hours = ("0" + date.getHours()).slice(-2);
            const minutes = ("0" + date.getMinutes()).slice(-2);
            const seconds = ("0" + date.getSeconds()).slice(-2);
            const now = year + month + day + "T" + hours + minutes + seconds;

            const imageWithoutBoundingBox = canvas.toDataURL("image/jpeg");
            if (boxes.length > 0) {
                draw_boxes(context, boxes);
                if (boxes.some(box => box.Probability > 0.75)) {
                    const imageWithBoundingBox = canvas.toDataURL("image/jpeg");
                    send_result(`event`, now, imageWithBoundingBox, boxes);
                } else {
                    send_result(`query`, now, imageWithoutBoundingBox, boxes);
                }
            } else {
                send_image(now, imageWithoutBoundingBox);
            }

            const input = preprocess_input(canvas);
            worker.postMessage(input);
        }, 100);
    });

    worker.onmessage = (event) => {
        const output = event.data;
        const canvas = document.querySelector("canvas");
        boxes = process_output(output, canvas.width, canvas.height);
    };
}

function preprocess_input(image) {
    const [image_width, image_height] = [640, 640];
    const canvas = document.createElement("canvas");
    canvas.width = image_width;
    canvas.height = image_height;
    const context = canvas.getContext("2d");

    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const imageData = context.getImageData(0, 0, canvas.width, canvas.height).data;

    const red = [], green = [], blue = [];
    for (let i = 0; i < imageData.length; i += 4) {
        red.push(imageData[i] / 255);
        green.push(imageData[i + 1] / 255);
        blue.push(imageData[i + 2] / 255);
    }

    return [...red, ...green, ...blue];
}

function process_output(output, image_width, image_height) {
    const yolo_classes = [
        'person', 'bicycle', 'car', 'motorcycle', 'airplane', 'bus', 'train', 'truck', 'boat',
        'traffic light', 'fire hydrant', 'stop sign', 'parking meter', 'bench', 'bird', 'cat', 'dog', 'horse',
        'sheep', 'cow', 'elephant', 'bear', 'zebra', 'giraffe', 'backpack', 'umbrella', 'handbag', 'tie', 'suitcase',
        'frisbee', 'skis', 'snowboard', 'sports ball', 'kite', 'baseball bat', 'baseball glove', 'skateboard',
        'surfboard', 'tennis racket', 'bottle', 'wine glass', 'cup', 'fork', 'knife', 'spoon', 'bowl', 'banana', 'apple',
        'sandwich', 'orange', 'broccoli', 'carrot', 'hot dog', 'pizza', 'donut', 'cake', 'chair', 'couch', 'potted plant',
        'bed', 'dining table', 'toilet', 'tv', 'laptop', 'mouse', 'remote', 'keyboard', 'cell phone', 'microwave', 'oven',
        'toaster', 'sink', 'refrigerator', 'book', 'clock', 'vase', 'scissors', 'teddy bear', 'hair drier', 'toothbrush'
    ];

    let boxes = [];

    for (let i = 0; i < 8400; i++) {
        const [class_id, prob] = [...Array(yolo_classes.length).keys()]
            .map(col => [col, output[8400 * (col + 4) + i]])
            .reduce((accum, item) => item[1] > accum[1] ? item : accum, [0, 0]);

        if (prob < 0.5) {
            continue;
        }

        const label = yolo_classes[class_id];
        const xc = output[i];
        const yc = output[8400 + i];
        const w = output[2 * 8400 + i];
        const h = output[3 * 8400 + i];
        const x1 = (xc - w / 2) / 640 * image_width;
        const y1 = (yc - h / 2) / 640 * image_height;
        const x2 = (xc + w / 2) / 640 * image_width;
        const y2 = (yc + h / 2) / 640 * image_height;
        boxes.push([x1, y1, x2, y2, label, prob]);
    }
    boxes = boxes.sort((box1, box2) => box2[5] - box1[5]);

    const result = [];
    while (boxes.length > 0) {
        result.push(boxes[0]);
        boxes = boxes.filter(box => iou(boxes[0], box) < 0.7 || boxes[0][4] !== box[4]);
    }

    return result;
}

function iou(box1, box2) {
    return intersection(box1, box2) / union(box1, box2);
}

function union(box1, box2) {
    const [box1_x1, box1_y1, box1_x2, box1_y2] = box1;
    const [box2_x1, box2_y1, box2_x2, box2_y2] = box2;
    const box1_area = (box1_x2 - box1_x1) * (box1_y2 - box1_y1);
    const box2_area = (box2_x2 - box2_x1) * (box2_y2 - box2_y1);

    return box1_area + box2_area - intersection(box1, box2);
}

function intersection(box1, box2) {
    const [box1_x1, box1_y1, box1_x2, box1_y2] = box1;
    const [box2_x1, box2_y1, box2_x2, box2_y2] = box2;
    const x1 = Math.max(box1_x1, box2_x1);
    const y1 = Math.max(box1_y1, box2_y1);
    const x2 = Math.min(box1_x2, box2_x2);
    const y2 = Math.min(box1_y2, box2_y2);

    return (x2 - x1) * (y2 - y1);
}

function draw_boxes(context, boxes) {
    context.strokeStyle = "#00FF00";
    context.lineWidth = 3;
    context.font = "18px serif";

    boxes.forEach(([x1, y1, x2, y2, label]) => {
        context.strokeRect(x1, y1, x2 - x1, y2 - y1);
        context.fillStyle = "#00ff00";
        const width = context.measureText(label).width;
        context.fillRect(x1, y1, width + 10, 25);
        context.fillStyle = "#000000";
        context.fillText(label, x1, y1 + 18);
    });
}

function send_result(topic, now, image, boxes) {
    const boxesObjectArray = boxes.map(box => {
        return {
            X1: box[0],
            Y1: box[1],
            X2: box[2],
            Y2: box[3],
            Label: box[4],
            Probability: box[5]
        };
    });
    const content = {
        Date: now,
        UserId: id,
        Image: image,
        BoundingBoxes: boxesObjectArray,
    };
    get_message_size(content);
    message = new Paho.MQTT.Message(JSON.stringify(content));
    message.destinationName = topic;
    if (mqtt_client && mqtt_client.isConnected()) {
        console.time(`${message.destinationName}으로 메시지가 전송됐습니다`);
        mqtt_client.send(message);
        console.timeEnd(`${message.destinationName}으로 메시지가 전송됐습니다`);

    } else {
        console.log(`연결된 클라이언트가 없습니다.`);
    }
}

function send_image(now, image) {
    const content = {
        Date: now,
        UserId: id,
        Image: image
    };
    get_message_size(content);
    message = new Paho.MQTT.Message(JSON.stringify(content));
    message.destinationName = `image-${id}`;
    if (mqtt_client && mqtt_client.isConnected()) {
        console.time(`${message.destinationName}으로 메시지가 전송됐습니다`);
        mqtt_client.send(message);
        console.timeEnd(`${message.destinationName}으로 메시지가 전송됐습니다`);
    } else {
        console.log(`연결된 클라이언트가 없습니다.`);
    }
}

function get_message_size(jsonObject) {
    const jsonString = JSON.stringify(jsonObject);
    const blob = new Blob([jsonString], { type: 'text/plain' });
    const reader = new FileReader();
    reader.onload = () => {
        const messageSize = reader.result.byteLength;
        console.log(`메시지 크기: ${messageSize} bytes`);
    }
    reader.readAsArrayBuffer(blob);
}


let id;
let mqtt_client;

let intervalId;


function connect_mqtt_client(client_id) {
    id = client_id;
    host = `ictrobot.hknu.ac.kr`;
    port = 8090;

    mqtt_client = new Paho.MQTT.Client(host, Number(port), `client-${id}`);
    mqtt_client.connect({
        useSSL: true,
        onSuccess: () => console.log(`client-${id}(이)가 연결됐습니다.`)
    });
}

function unload() {
    if (worker) {
        worker.onmessage = null;
        worker = null;
    }

    if (intervalId) {
        clearInterval(intervalId);
    }

    if (mqtt_client && mqtt_client.isConnected()) {
        mqtt_client.disconnect();
    }
}
