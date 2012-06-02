var http = require('http'),
	sys = require('sys'),
	fs = require('fs');

var server = http.createServer(function(request, response){

	fs.readFile('./clientTest.js',function(err, data){
		if(err) {
			throw err;
		}

		response.writeHeader(200, {"Content-Type": "text/html"});
		response.end(data);
	});

})

server.listen(8000);

var io = require('socket.io').listen(server);

io.sockets.on('connection', function (socket) {
  socket.emit('news', { hello: 'world' });
  socket.on('my other event', function (data) {
    console.log(data);
  });
});
