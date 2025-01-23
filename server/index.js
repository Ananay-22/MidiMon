const WebSocket = require('ws');

// Create a WebSocket server on port 8080
const wss = new WebSocket.Server({ port: 8080 });

let clients = [];

// When a new client connects
wss.on('connection', (ws) => {

    // Add the new client to the list
    clients.push(ws);
    console.log(`New client connected. Total clients: ${clients.length}`);

    // When the client sends a message, forward it to every other client
    ws.on('message', (message) => {
        // Broadcast to all clients except the sender
        clients.forEach(client => {
            if (client !== ws && client.readyState === WebSocket.OPEN) {
                client.send(message);
            }
        });
    });

    // When the client disconnects (or is closed unexpectedly)
    ws.on('close', () => {
        clients = clients.filter(client => client !== ws);
        console.log(`Client disconnected. Total clients: ${clients.length}`);
    });

    // Optional: Handle errors (e.g., unexpected closure)
    ws.on('error', (error) => {
        console.error(`Error with a client: ${error}`);
    });
});

// Periodic check for disconnected clients (heartbeat)
setInterval(() => {
    clients.forEach((client, index) => {
        if (client.readyState === WebSocket.CLOSING || client.readyState === WebSocket.CLOSED) {
            clients.splice(index, 1);
            console.log(`Client removed due to disconnection.`);
        }
    });
}, 5000); // Check every 5 seconds

console.log('WebSocket server started on ws://localhost:8080');
