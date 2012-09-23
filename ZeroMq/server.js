var zeromq = require("zmq");

var socket = zeromq.socket('push');

socket.bind("tcp://*:12345", function(err)
{

	if(err) throw err;

	console.log("bound");
	setInterval(function()
	{

		socket.send('{"oldValue" : "hell", "newValue": "hello"} ');

		socket.send('{"oldValue" : "worl", "newValue": "world"} ');

		console.log("Sent: hello world");

	}, 100);

});