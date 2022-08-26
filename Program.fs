open System
open System.Xml
open System.Collections.Generic
open System.Security.Cryptography
open Suave
open Suave.Filters
open Suave.Operators
open Suave.Successful
open Suave.Writers
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Newtonsoft.Json
open Akka.Actor
open Akka.FSharp

let setCORSHeaders =
    setHeader  "Access-Control-Allow-Origin" "*"
    >=> setHeader "Access-Control-Allow-Headers" "content-type"

let rsa = new RSACryptoServiceProvider(2048)
let system = ActorSystem.Create("TwitterEngine")

let mutable users = new Dictionary<string, string>()
let mutable onlineUsers = new Dictionary<string, bool>()
let mutable tweetDict = new Dictionary<string, List<string>>()
let mutable followers = new Dictionary<string, List<string>>()
let mutable hashTags = Map.empty
let mutable mentions = new Dictionary<string, Dictionary<string, List<string>>>()
let mutable wbsockReference = Map.empty

// Request Types
type Register =
    {
        UserName: string
        Password: string
    }

type Login =
    {
        UserName: string
        Password: string
    }

type Logout =
    {
        UserName: string
    }

type Follower = 
    {
        Follower: string
        Following: string
    }

type PostTweet =
    {
        Tweet: string
        UserName: string
    }

// ResponseType
type ResponseMessage =
    {
        UserID: string
        Message: string
        Content: list<string>
        Check: int
        error: bool
    }
      
type ActorMessages =
    | AddTweetDict of PostTweet
    | AddTweetFollowerDict of PostTweet
    | ParseTweet of PostTweet
    | NewTweetToOther of WebSocket*PostTweet
    | NewMentionTweet of WebSocket* PostTweet
    | TweetFeed of WebSocket * PostTweet
    | Following of WebSocket * string


let byteToScreen (message:string) =
    message
    |> System.Text.Encoding.ASCII.GetBytes
    |> ByteSegment

let byteConvertToString x = 
    BitConverter.ToString(x)

let stringConvertToByte (str: string) = 
    System.Text.Encoding.ASCII.GetBytes(str)

let hashToSHA256 (str: string) = 
    let bitArrayOfStr = stringConvertToByte str  
    let hashOutput = HashAlgorithm.Create("SHA256").ComputeHash bitArrayOfStr
    let hashStr = byteConvertToString hashOutput
    hashStr

let regUser (user: Register) =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   Register"
    printfn "User           :   %s" user.UserName
    printfn "JSON           :   %A" user
    if users.ContainsKey user.UserName then
        {UserID = user.UserName;Message = "User by this username already exists";Content=[];Check=1;error=true}
    else
        users.Add(user.UserName, user.Password)
        {UserID = user.UserName;Message = "You have been successfully registered";Content=[];Check=1;error=false}

let loginuser (user: Login) = 
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   Login"
    printfn "User           :   %s" user.UserName
    printfn "JSON           :   %A" user
    if users.ContainsKey user.UserName then
        if users.Item(user.UserName) = user.Password then
            if onlineUsers.ContainsKey user.UserName then
                {UserID = user.UserName;Message = "You are already logged in";Content=[];Check=2;error=true}
            else
                onlineUsers.Add(user.UserName, true)
                {UserID = user.UserName;Message = "You have been logged in";Content=[];Check=2;error=false} 
        else
            printfn "Error caught"
            {UserID = user.UserName;Message = "Password not correct";Content=[];Check=1;error=true} 
    else
        {UserID = user.UserName;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}                  

let logoutuser (user:Logout) = 
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   Logout"
    printfn "User           :   %s" user.UserName
    printfn "JSON           :   %A" user
    if users.ContainsKey user.UserName then
        if onlineUsers.ContainsKey user.UserName then
            onlineUsers.Remove(user.UserName) |> ignore
            {UserID = user.UserName;Message = "You have been logged out";Content=[];Check=1;error=false}
        else
            {UserID = user.UserName;Message = "You were not logged in";Content=[];Check=1;error=true}
    else
        {UserID = user.UserName;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}        

let isLoggedIn username =  
    if onlineUsers.ContainsKey username then
        "User Logged In" // Logged In
    else    
        if users.ContainsKey username then
            "User Not Logged In" // User Exsists but not logged in 
        else    
            "Not Registered" // User Doesn't Exsist


let isRegistered username =
    if users.ContainsKey username then
        true
    else 
        false      

let twitterFeedHandler (mailbox:Actor<_>) = 
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with
        |TweetFeed(ws,tweet)->  
            let response = "Your Tweet: "+tweet.Tweet
            let byteResponse = byteToScreen response
            let s =socket{
                            do! ws.send Text byteResponse true
                            }
            Async.StartAsTask s |> ignore
        |NewTweetToOther(ws,tweet)->
            let response = tweet.UserName+" has tweeted: "+tweet.Tweet
            let byteResponse = byteToScreen response
            let s =socket{
                            do! ws.send Text byteResponse true
                            }
            Async.StartAsTask s |> ignore
        |NewMentionTweet(ws,tweet)->
            let response = tweet.UserName+" mentioned you in his tweet: "+tweet.Tweet
            let byteResponse = byteToScreen response
            let s =socket{
                            do! ws.send Text byteResponse true
                            }
            Async.StartAsTask s |> ignore
        |Following(ws,msg)->
            let response = msg
            let byteResponse = byteToScreen response
            let s = socket{
                            do! ws.send Text byteResponse true
                            }
            Async.StartAsTask s |> ignore
        return! loop()
    }
    loop()
    
let twitterFeed = spawn system "boss" twitterFeedHandler

let addFollower (follower: Follower) =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   FollowUser"
    printfn "User           :   %s" follower.Follower
    printfn "Following      :   %s" follower.Following
    printfn "JSON           :   %A" follower
    let checkOnline = isLoggedIn follower.Follower
    if checkOnline = "User Logged In" then
        if isRegistered follower.Following then
            let temp1 = wbsockReference.TryFind(follower.Follower)
            if followers.ContainsKey follower.Following then
                if followers.Item(follower.Following).Contains(follower.Follower) then
                    if temp1 <> None then
                        twitterFeed <! Following(temp1.Value,"You are already following: "+follower.Following)
                    {UserID = follower.Follower;Message = "You are already Following"+follower.Following;Content=[];Check=2;error=true}
                else
                    followers.Item(follower.Following).Add(follower.Follower)
                    if temp1 <> None then
                        twitterFeed <! Following(temp1.Value,"You are now following: "+follower.Following)
                    {UserID = follower.Follower;Message = "Sucessfully Added to the Following list";Content=[];Check=2;error=false}
            else
                let mutable followerList = new List<string>()
                followerList.Add(follower.Follower)
                followers.Add(follower.Following, followerList)
                if temp1 <> None then
                    twitterFeed <! Following(temp1.Value,"You are now following: "+follower.Following)
                {UserID = follower.Follower;Message = "Sucessfully Added to the Following list";Content=[];Check=2;error=false}
        else
            {UserID = follower.Follower;Message = "Follower "+follower.Following+" doesn't exsist";Content=[];Check=2;error=true}
    elif checkOnline = "User Not Logged In" then
        {UserID = follower.Follower;Message = "Please Login";Content=[];Check=1;error=true}
    else
        {UserID = follower.Follower;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}

let addTweet (tweet: PostTweet) =
    if tweetDict.ContainsKey tweet.UserName then
        tweetDict.Item(tweet.UserName).Add(tweet.Tweet)
    else
        let tweetList = new List<string>()
        tweetList.Add(tweet.Tweet)
        tweetDict.Add(tweet.UserName, tweetList)    

let addTweetToFollowers (tweet: PostTweet) = 
    if followers.ContainsKey tweet.UserName then
        for i in followers.Item(tweet.UserName) do
            let tweetForFollower = {Tweet=tweet.Tweet;UserName=i}
            addTweet tweetForFollower
            let temp2 = wbsockReference.TryFind(i)
            printfn "%s" i
            if temp2 <> None then
                twitterFeed <! NewTweetToOther(temp2.Value,tweet)
   

let tweetParser (tweet:PostTweet) =
    let splits = (tweet.Tweet.Split ' ')
    for i in splits do
        if i.StartsWith "@" then
            let mentionString = i.Split '@'
            if isRegistered mentionString.[1] then
                if mentions.ContainsKey mentionString.[1] then
                    if mentions.Item(mentionString.[1]).ContainsKey tweet.UserName then
                        mentions.Item(mentionString.[1]).Item(tweet.UserName).Add(tweet.Tweet)
                    else 
                        let twtList = new List<string>()
                        twtList.Add(tweet.Tweet)
                        mentions.Item(mentionString.[1]).Add(tweet.UserName, twtList)
                else
                    let mutable mentionDict = new Dictionary<string, List<string>>()
                    let mutable tweetList = new List<string>()
                    tweetList.Add(tweet.Tweet)
                    mentionDict.Add(tweet.UserName,tweetList)
                    mentions.Add(mentionString.[1],mentionDict)
                let temp3 = wbsockReference.TryFind(mentionString.[1])
                if temp3<>None then
                    twitterFeed <! NewMentionTweet(temp3.Value,tweet)            
        elif i.StartsWith "#" then
            let hashtagString = i.Split '#'
            let htag = hashTags.TryFind(hashtagString.[1])
            if htag = None then
                let hlist = List<string>()
                hlist.Add(tweet.Tweet)
                hashTags <- hashTags.Add(hashtagString.[1],hlist)
            else
                htag.Value.Add(tweet.Tweet)


let tweetHandler (mailbox:Actor<_>) =
    let rec loop() = actor{
        let! msg = mailbox.Receive()
        match msg with 
        | AddTweetDict(tweet) -> 
            addTweet(tweet)
            let wbref = wbsockReference.TryFind(tweet.UserName)
            if wbref <> None then
                twitterFeed <! TweetFeed(wbref.Value,tweet)
        | AddTweetFollowerDict(tweet) ->  
            addTweetToFollowers(tweet)
        | ParseTweet(tweet) -> 
            tweetParser(tweet)
        return! loop()
    }
    loop()

let tweetHandlerRef = spawn system "tweetParserActor" tweetHandler

let addToTweetList (tweet: PostTweet) =
    let checkOnline = isLoggedIn tweet.UserName
    if checkOnline = "User Logged In" then
        tweetHandlerRef <! AddTweetFollowerDict(tweet)
        tweetHandlerRef <! ParseTweet(tweet)
        tweetHandlerRef <! AddTweetDict(tweet)
        {UserID = tweet.UserName;Message = "Tweeted Succesfully";Content=[];Check=2;error=false}
    elif checkOnline = "User Not Logged In" then
        {UserID = tweet.UserName;Message = "Please Login";Content=[];Check=1;error=true}
    else
        {UserID = tweet.UserName;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}

let fetchHashtags username hashtag =
    let checkOnline = isLoggedIn username
    if checkOnline = "User Logged In" then
        printf "%s" hashtag
        let temp = hashTags.TryFind(hashtag)
        if temp = None then
            {UserID = username;Message = "No Tweets with this hashtag found";Content=[];Check=2;error=false}
        else
            let len = Math.Min(10,temp.Value.Count)
            let res = [for i in 1 .. len do yield(temp.Value.[i-1])] 
            {UserID = username;Message = "FetchHashtags Successfull";Content=res;Check=2;error=false}
    elif checkOnline = "User Not Logged In" then
        {UserID = username;Message = "Please Login";Content=[];Check=1;error=true}
    else
        {UserID = username;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}

let fetchTweets username =
    let checkOnline = isLoggedIn username
    if checkOnline = "User Logged In" then
        if tweetDict.ContainsKey username then
            let len = Math.Min(10,tweetDict.Item(username).Count)
            let fetchTweetList = [for i in 0 .. len-1 do yield(tweetDict.Item(username).[i])] 
            {UserID = username;Message = "FetchTweets Successfull";Content=fetchTweetList;Check=2;error=false}
        else    
            {UserID = username;Message = "No Tweets";Content=[];Check=2;error=false}
    elif checkOnline = "User Not Logged In" then
        {UserID = username;Message = "Please Login";Content=[];Check=1;error=true}
    else
        {UserID = username;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}                
    
let fetchMentions username = 
    let checkOnline = isLoggedIn username
    let resp = new List<string>()
    if checkOnline = "User Logged In" then
        if mentions.ContainsKey username then
            for i in mentions.Item(username) do
                for j in mentions.Item(username).Item(i.Key) do
                    resp.Add(j)
            let len = Math.Min(10,resp.Count)        
            let resp = [for i in 1 .. len do yield(resp.[i-1])] 
            {UserID = username;Message = "FetchMentions Successfull";Content=resp;Check=2;error=false}
        else
            {UserID = username;Message = "No Mentions";Content=[];Check=2;error=false}
    elif checkOnline = "User Not Logged In" then
        {UserID = username;Message = "Please Login";Content=[];Check=1;error=true}
    else
        {UserID = username;Message = "User by these credentials is not registered";Content=[];Check=0;error=true}

let postNewTweet (tweet: PostTweet) =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   PublishNewTweet"
    printfn "User           :   %s" tweet.UserName
    printfn "Tweet          :   %s" tweet.Tweet
    printfn "JSON           :   %A" tweet
    addToTweetList tweet

let postReTweet (tweet: PostTweet) =
    let len = tweetDict.Item(tweet.UserName).Count
    let userReTweet = tweetDict.Item(tweet.UserName).[len-1]
    let retweet = {UserName = tweet.UserName; Tweet = userReTweet}
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   PublishReTweet"
    printfn "User           :   %s" tweet.UserName
    printfn "Tweet          :   %s" retweet.Tweet
    printfn "JSON           :   %A" tweet
    addToTweetList retweet

//low level functions
let getString (rawForm: byte[]) =
    System.Text.Encoding.UTF8.GetString(rawForm)

let fromJson<'a> json =
    JsonConvert.DeserializeObject(json, typeof<'a>) :?> 'a

//bridge functions between routes
let publicKey username = 
    let publicKeyString = rsa.ToXmlString(false)

    let doc = new XmlDocument()
    doc.LoadXml(publicKeyString)

    let xpath = "RSAKeyValue/Modulus"  
    // let node = new XmlNode()
    let node = doc.SelectSingleNode(xpath)
    let valueNode = node.InnerText
    printfn "Received PublicKey Request %s" valueNode
    {UserID = username;Message = "Public Key Sent Successfully";Content=[valueNode];Check=2;error=false}
    
let getpublickey username =  
    publicKey username
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let register =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Register>
    |> regUser
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let login =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Login>
    |> loginuser
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let logout =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Logout>
    |> logoutuser
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let postTweet = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<PostTweet>
    |> postNewTweet
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let retweet = 
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<PostTweet>
    |> postReTweet
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let follow =
    request (fun r ->
    r.rawForm
    |> getString
    |> fromJson<Follower>
    |> addFollower
    |> JsonConvert.SerializeObject
    |> OK
    )
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let fetchtweets username =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   FetchTweets"
    printfn "User           :   %s" username
    fetchTweets username
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let fetchmentions username =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   FetchMentions"
    printfn "User           :   %s" username
    fetchMentions username
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders

let fetchhashtags username hashtag =
    printfn "--------------------------------------------------------------------"
    printfn "Request Type   :   FetchHashtags"
    printfn "User           :   %s" username
    printfn "Hashtag        :   %A" hashtag
    fetchHashtags username hashtag
    |> JsonConvert.SerializeObject
    |> OK
    >=> setMimeType "application/json"
    >=> setCORSHeaders


let websocketHandler (webSocket : WebSocket) (context: HttpContext) =
    socket {
        let mutable loop = true

        while loop do
              let! msg = webSocket.read()

              match msg with
              | (Text, data, true) ->
                let str = UTF8.toString data 
                if str.StartsWith("UserName:") then
                    let userid = str.Split(':').[1]
                    wbsockReference <- wbsockReference.Add(userid,webSocket)
                    printfn "--------------------------------------------------------------------"
                    printfn "%s is now live" userid
                else
                    let response = sprintf "response to %s" str
                    let byteResponse = byteToScreen response
                    do! webSocket.send Text byteResponse true

              | (Close, _, _) ->
                let emptyResponse = [||] |> ByteSegment
                do! webSocket.send Close emptyResponse true
                loop <- false
              | _ -> ()
    }

let allow_cors : WebPart =
    choose [
        OPTIONS >=>
            fun context ->
                context |> (
                    setCORSHeaders
                    >=> OK "CORS approved" )
    ]

//setup app routes
let app =
    choose
        [ 
            path "/websocket" >=> handShake websocketHandler 
            allow_cors
            GET >=> choose
                [ 
                path "/" >=> OK "Hello World"
                pathScan "/fetchtweets/%s" (fun username -> (fetchtweets username))
                pathScan "/fetchmentions/%s" (fun username -> (fetchmentions username))
                pathScan "/fetchhashtags/%s/%s" (fun (username,hashtag) -> (fetchhashtags username hashtag))
                ]

            POST >=> choose
                [   
                // path "/publickey" >=> publickey
                path "/posttweet" >=> postTweet 
                path "/retweet" >=> retweet 
                path "/register" >=> register
                path "/login" >=> login
                path "/logout" >=> logout
                path "/follow" >=> follow
                ]

            PUT >=> choose
                [ ]

            DELETE >=> choose
                [ ]
        ]

[<EntryPoint>]
let main argv =
    startWebServer defaultConfig app
    0