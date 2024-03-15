Bogers.Chapoco.Publisher
  Background service, watches file(s) (periodic poll?) -> publish to configured services
  service can monitor certain characteristics, when said characteristics (start off with header) change
  publish target (sink?) can be an http endpoint, a messagequeue, a file, pushover, whatever
  we'll start off with an http endpoint and pushover

Bogers.Chapoco.FlowCleaner
  App running on a schedule, cleans mitmproxy flow files

Bogers.Chapoco.Api
  API for exposing pocacha api, allows browser usage without having achieved a level 20 account

  We'll start off with the following:
    * Password auth to authenticate myself <-- chapoco will be public facing, dont want others to actually make use of it lmao
    * Token (?) auth for publisher

    * Internal only, endpoint for getting tokens delivered via publisher
    * Endpoint for sending *whatever* (post/get request, configure body), basically 'debug' mode
    * Endpoint for listing current following -> filtered by currently online (display profile + link to view)
    * Endpoint for viewing a stream
    * Endpoint for reading comments (polled by frontend on 1-5s interval?, push comments via websocket?)
    * Endpoint for posting/publishing comments