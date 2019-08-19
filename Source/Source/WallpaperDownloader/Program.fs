open System.IO
open System.Net.Http
open System.Xml

let basicUri i n = sprintf "https://www.bing.com/HPImageArchive.aspx?format=xml&idx=%d&n=%d&mkt=zh-cn" i n

let imageUri i = sprintf "https://www.bing.com/%s_1920x1080.jpg" i

let fetchElement (client : HttpClient) i n = async {
    let uri = basicUri i n
    let! xml = client.GetStringAsync uri |> Async.AwaitTask
    let document = XmlDocument()
    document.LoadXml xml
    return document.SelectNodes("/images/image") |> Seq.cast<XmlElement>
}

let fetchDocument (client : HttpClient) = async {
    let fetch = fetchElement client
    let! a = fetch 0 7
    let! b = fetch 7 8
    return [a; b] |> Seq.concat |> Seq.toList
}

let downloadImage (client : HttpClient) query path = async {
    let uri = imageUri query
    let! buffer = client.GetByteArrayAsync uri |> Async.AwaitTask
    use stream = new FileStream(path, FileMode.CreateNew)
    do! stream.AsyncWrite buffer
}

let downloadOrSkip (client : HttpClient) folder (xml : XmlElement) = 
    let info = DirectoryInfo folder
    if not info.Exists then
        info.Create()
    let date = xml.SelectSingleNode("enddate").InnerText
    let path = Path.Combine(info.FullName, sprintf "%s.jpg" date) |> FileInfo
    async {
        if path.Exists then
            printfn "%s skipped" path.Name
        else
            printfn "%s downloading ..." path.Name
            let text = xml.SelectSingleNode("urlBase").InnerText
            do! downloadImage client text path.FullName
        }

let fetch client folder = async {
    let! list = fetchDocument client
    for i in list do
        do! downloadOrSkip client folder i
}

[<EntryPoint>]
let main _ =
    let folder = "image"
    use client = new HttpClient()
    try
        fetch client folder |> Async.RunSynchronously
    with
    | ex -> printfn "%O" ex
    0
