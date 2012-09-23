var zeromq = require("zmq");

var socket = zeromq.socket('pull');

socket.connect("tcp://127.0.0.1:12345");


console.log("connected");


socket.on('message', function(data)
{

	var dataObj = JSON.parse(data.toString());

	console.log("received old: "+ dataObj.oldValue +"new "+ dataObj.newValue);

});

