// -------------- SETTINGS AREA --------------- \\

const port     = 8888; // Must be same as in Rainmeter plugin
const timer    = 60 * 1000; // Send broadcast every minute


// -------------- CONSTANTS AREA -------------- \\

const os       = require('os');
const requestForBroadcast = 'CollectingBroadcasts';
const broadcastMessage    = Buffer.from(`hostname:${os.hostname()};`, 'ascii');

const dgram    = require('dgram');
const server   = dgram.createSocket('udp4');

server.on('error', (err) => {
	  console.log(`server error:\n${err.stack}`);
	  server.close();
});

server.on('message', (msg, rinfo) => {
    if (requestForBroadcast === msg.toString('ascii')) {
	 sendMessage(broadcastMessage, rinfo.address);
    }
});

server.on('listening', () => {
    const address = server.address();
    server.setBroadcast(true);
    
    sendMessage(broadcastMessage, '255.255.255.255');
    
    setInterval(() => {
        sendMessage(broadcastMessage, '255.255.255.255');
    }, timer);

    console.log(`server listening ${address.address}:${address.port}`);
});

function sendMessage(msg, address) {
         server.send(msg, 0, msg.length, port, address);
}

server.bind(port);
