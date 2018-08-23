open System
open System.Text.RegularExpressions
open System.IO
open System.Reactive.Linq

let syncAndWait (filter: string -> bool) (srcRoot: string) (destRoot: string) =
    
    let w = new FileSystemWatcher(Path = srcRoot, IncludeSubdirectories = true)
    
    let inline fullPath (arg: ^T) = (^T : (member FullPath: string) arg)

    let observableFS (e: IEvent<_, FileSystemEventArgs>) = (e :> IObservable<FileSystemEventArgs>).Select(fullPath)
    let observableRN (e: IEvent<_, RenamedEventArgs>)    = (e :> IObservable<RenamedEventArgs>)   .Select(fullPath)

    let log (ts: DateTime) src dest = printfn "%s\r\n%s\r\n-> %s \r\n" (ts.ToString("HH:mm:ss")) src dest


    Observable.Merge([observableFS w.Created; observableFS w.Changed; observableRN w.Renamed])
              .Where(filter)
              .GroupBy(fun x -> x)
              .SelectMany(fun x -> x.Throttle(TimeSpan.FromSeconds(0.1)))
              .Synchronize()
              .Subscribe(fun srcFilePath ->
                   let destFilePath = destRoot + srcFilePath.Substring(srcRoot.Length)
                   let dir = FileInfo(destFilePath).Directory;
               
                   if not <| dir.Exists then
                      Directory.CreateDirectory(dir.FullName) |> ignore
               
                   File.Copy(srcFilePath, destFilePath, true)
               
                   log DateTime.Now srcFilePath destFilePath)
              |> ignore      

    w.EnableRaisingEvents <- true

    Console.ReadLine() |> ignore
    w.Dispose()
    
    ()
    
[<EntryPoint>]
let main argv =
    try
        // argv.[0] Filter Path Filter Regex
        // argv.[1] Source Directory Path
        // argv.[2] Destination Directory Path

        let cleanDirPath (str: string) =
            if not <| str.EndsWith('\\')
            then str + "\\"
            else str

        let regex = Regex(argv.[0])
        let srcDirPath = cleanDirPath argv.[1]
        let destDirPath = cleanDirPath argv.[2]

        printfn "Regex       : %s" (regex.ToString())
        printfn "Source      : %s" srcDirPath
        printfn "Destination : %s" destDirPath
        
        syncAndWait (fun x -> regex.IsMatch(x)) srcDirPath destDirPath
    with
        | ex -> printfn "Exception %s" (ex.Message)

    Console.ReadLine() |> ignore
        
    0 // return an integer exit code
