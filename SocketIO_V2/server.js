//var app = require('http').createServer(handler)
var express = require('express')
  , app = express()
  , http = require('http').createServer(app)
  , io = require('socket.io').listen(http)
  , fs = require('fs')


http.listen(80);

app.use(express.static(__dirname));
app.use(clientErrorHandler);
app.use(errorHandler);

function clientErrorHandler(err, req, res, next)
{

  res.status(500);
  res.render('error', { error: err });

}

function errorHandler(err, req, res, next)
{

  if (req.xhr) {
    res.send(500, { error: 'Something blew up!' });
  } else {
    next(err);
  }

}

app.get("/", function(req, res){
  
  res.redirect("test.html");

});



function handler (req, res) {
  fs.readFile(__dirname + '/test.html',
  function (err, data) {
    if (err) {
      res.writeHead(500);
      return res.end('Error loading index.html');
    }

    res.writeHead(200);
    res.end(data);
  });
}

io.sockets.on('connection', function (socket) {
  socket.emit('welcome', { msg: 'hello world' });
  
  socket.on('toUpper', function (data) {
    console.log(data);

    socket.emit('result', {result : data.msg.toUpperCase()});

  });

  socket.on('toLower', function (data) {
    console.log(data);

    socket.emit('result', {result : data.msg.toLowerCase()});
  });
});
