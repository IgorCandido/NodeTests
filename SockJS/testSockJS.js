var http = require('http'),
	sockjs = require('sockjs'),
	node_static = require('node-static');

var sockjs_opts = {sockjs_url: "http://cdn.sockjs.org/sockjs-0.3.min.js"};

var echo = sockjs.createServer(sockjs_opts);

echo.on('connection', function(conn)
{

	conn.on('data', function(message)
	{

		conn.write(message);

	});

	conn.on('close', function() {});

});

var static_directory = new node_static.Server(__dirname);

var server = http.createServer();

server.addListener('request', function(req, res){
	static_directory.serve(req, res);
});

server.addListener('upgrade', function(req, res){
	res.end();
});

echo.installHandlers(server, {prefix: '/echo'});
server.listen(9999, '0.0.0.0');

console.log(' [*] Listening on 0.0.0.0:9999');