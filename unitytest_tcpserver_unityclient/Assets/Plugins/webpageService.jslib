mergeInto(LibraryManager.library, {

  SendMessageToBrowser: function (message) {
    MessageReceivedFromUnity(Pointer_stringify(message));
  },

  WebGLStartGame: function() {
    GameInitialized();
  },

  //SendNetworkMessageToServer: function(utf8bytes) {
  SendNetworkMessageToServer: function(jsonString) {
    //PipeGameMessageToServer(utf8bytes);
    PipeGameMessageToServer(Pointer_stringify(jsonString));
  },

  // ConnectWebglToServer: function() {
  //   console.log("calling webgl conncet");
  //   Connect();
  // }
  RegisterCallBackToWebgl: function (gameobjectName, onConnectUnityCallback, onRecieveNetworkMessageCallback) {
    console.log("calling webgl registercallback");
    RegisterCallBack(Pointer_stringify(gameobjectName), Pointer_stringify(onConnectUnityCallback), Pointer_stringify(onRecieveNetworkMessageCallback));
  },

  ConnectWebglToServer: function(https, ipadress, username) {
    console.log("calling webgl conncet");
    Connect(https, Pointer_stringify(ipadress), Pointer_stringify(username));
  }
});