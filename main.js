var username = ""
var publicKey = "MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCz1zqQHtHvKczHh58ePiRNgOyiHEx6lZDPlvwBTaHmkNlQyyJ06SIlMU1pmGKxILjT7n06nxG7LlFVUN5MkW/jwF39/+drkHM5B0kh+hPQygFjRq81yxvLwolt+Vq7h+CTU0Z1wkFABcTeQQldZkJlTpyx0c3+jq0o47wIFjq5fwIDAQAB";

const signUpButton = document.getElementById('signUp');
const signInButton = document.getElementById('signIn');
const container = document.getElementById('main');

signUpButton.addEventListener('click', () => {
    container.classList.add("right-panel-active");
});

signInButton.addEventListener('click', () => {
    container.classList.remove("right-panel-active");
});

function loginload(){
    
    document.getElementById("full").style.display="none";
    document.getElementById("logouthead").style.display="none";
    document.getElementById("reglogin").style.display="block";
    clearinput();
}

function userload(){
    
    document.getElementById("full").style.display="block";
    document.getElementById("logouthead").style.display="block";
    document.getElementById("reglogin").style.display="none";
    clearinput();
}
function clearinput(){
    var elements = document.getElementsByTagName("input");
    for (var ii=0; ii < elements.length; ii++) {
        elements[ii].value = "";
    }   
    elements = document.getElementsByTagName("textarea");
    for (var ii=0; ii < elements.length; ii++) {
        elements[ii].value = "";
    }
}
function getpublickey(){
    var url = "http://127.0.0.1:8080/getpublickey/"+username;
    clearinput();
    $.ajax({
        type: "GET",
        url: url,
        dataType: "json",
        success: function(data){
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment); 
            }else{
                // alert(data.Comment); 
                publicKey = data.Message[0]
            }                                     
        }
    });
}
function register(){
    // console.log("register");
    var url = "http://127.0.0.1:8080/register";
    var uname = document.getElementById("runame").value;
    var pwd = document.getElementById("rpass").value;
    // let RSAEncrypt = new JSEncrypt();
    // RSAEncrypt.setPublicKey(publicKey);
    // let encryptedPass = RSAEncrypt.encrypt(pwd);
    // let encryptedPassBase64 =  Buffer.from(pwd).toString('base64')
    var data = JSON.stringify({'UserName':uname,"Password":pwd});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            if(data.error){
                
            }else{
                // alert(data.Comment);
            }
            alert(data.Message);
            
        }
    });
    
    
}
function login(){
    var url = "http://127.0.0.1:8080/login";
    var uname = document.getElementById("luname").value;
    var pwd = document.getElementById("lpass").value;
    var data = JSON.stringify({'UserName':uname,"Password":pwd});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            if(data.error){
                if(data.Check == 0){
                    alert(data.Message);
                }else{
                    username = uname;
                    // alert(data.Comment);
                    startWebSocket();
                    userload();
                }
                // alert(data.Comment);  
            }else{
                username = uname;
                startWebSocket();
                userload();
                // alert(data.Comment); 
            }
            
        }
    });
    console.log(username)
}
function logout(){
    var url = "http://127.0.0.1:8080/logout";
    var data = JSON.stringify({'UserName':username});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            if(data.error){
                // alert(data.Comment); 
            }else{
                
                // alert(data.Comment); 
            }
            loginload();
            username = "";
        }
    });
}
function retweet(){
    var url = "http://127.0.0.1:8080/retweet";
    var retweet = document.getElementById("tweet").value;
    var data = JSON.stringify({'Tweet':retweet,'UserName':username});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment); 
            }else{
                // alert(data.Comment); 
            }
        
            
        }
    });
}
function tweet(){
    var url = "http://127.0.0.1:8080/posttweet";
    var tweett = document.getElementById("tweet").value;
    var data = JSON.stringify({'Tweet':tweett,'UserName':username});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment); 
            }else{
                // alert(data.Comment); 
            }
        
            
        }
    });
}
function follow(){
    var url = "http://127.0.0.1:8080/follow";
    var following = document.getElementById("follower").value;
    var data = JSON.stringify({'Follower':username,"Following":following});
    clearinput();
    $.ajax({
        type: "POST",
        url: url,
        data: data,
        dataType: "json",
        success: function(data){
            
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment);  
            }else{
                // username = uname;
                // alert(data.Comment); 
            }
            
        }
    });
}
function gethashtags(){
    var hashtag = document.getElementById("hashtag").value;
    var url = "http://127.0.0.1:8080/fetchhashtags/"+username+"/"+hashtag;
    clearinput();
    $.ajax({
        type: "GET",
        url: url,
        dataType: "json",
        success: function(data){
            
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment); 
            }else{
                // alert(data.Comment); 
                var text = "";
                for (i=0;i<data.Content.length;i++){
                    text += data.Content[i]+"\n"
                }
                document.getElementById("gethashtags").value = text
            }
            
            
        }
    });
}
function getmentions(){
    var url = "http://127.0.0.1:8080/fetchmentions/"+username;
    clearinput();
    $.ajax({
        type: "GET",
        url: url,
        dataType: "json",
        success: function(data){
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
                // alert(data.Comment); 
            }else{
                // alert(data.Comment); 
                var text = "";
                for (i=0;i<data.Content.length;i++){
                    text += data.Content[i]+"\n"
                }
                document.getElementById("getmentions").value = text
            }
            
            
        }
    });
}
function gettweets(){
    var url = "http://127.0.0.1:8080/fetchtweets/"+username;
    clearinput();
    $.ajax({
        type: "GET",
        url: url,
        dataType: "json",
        success: function(data){
            if(data.error){
                alert(data.Message);
                if(data.Check < 2){
                    loginload();
                }
            }else{
                var tweetList = "";
                for (i=0;i<data.Content.length;i++){
                    tweetList += data.Content[i]+"\n";
                }
                document.getElementById("gettweets").value = tweetList
            }                                     
        }
    });
}
var output;
function startWebSocket(){
    var wsUri = "ws://localhost:8080/websocket";
    websocket = new WebSocket(wsUri);
    output = document.getElementById("output");
    websocket.onopen = function(evt) { onOpen(evt) };
    websocket.onclose = function(evt) { onClose(evt) };
    websocket.onmessage = function(evt) { onMessage(evt) };
    websocket.onerror = function(evt) { onError(evt) };
}
function onOpen(evt)
{
    writeToScreen("CONNECTED TO TWITTER");
    var message = "UserName:"+username;
    doSend(message);
}

function onClose(evt)
{
    writeToScreen("DISCONNECTED FROM TWITTER");
}

function onMessage(evt)
{
    writeToScreen('<span style="color: blue;"> ' + evt.data+'</span>');
}

function onError(evt)
{
    writeToScreen('<span style="color: red;">ERROR:</span> ' + evt.data);
}

function doSend(message)
{
    // writeToScreen("SENT: " + message);
    console.log("sending"+message) ;
    websocket.send(message);
}

function writeToScreen(message)
{
    var pre = document.createElement("p");
    pre.style.wordWrap = "break-word";
    pre.innerHTML = message;
    output.appendChild(pre);
}
// application code here!