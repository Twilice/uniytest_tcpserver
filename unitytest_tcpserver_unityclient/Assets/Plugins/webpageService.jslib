mergeInto(LibraryManager.library, {

  SendMessageToBrowser: function (message) {
    MessageReceivedFromUnity(Pointer_stringify(message));
  },

  WebGLStartGame: function() {
    GameInitialized();
  },

  SendNetworkMessageToServer: function(utf8bytes) {
    PipeGameMessageToServer(utf8bytes);
  },

  ConnectWebglToServer: function(https, ipadress, username, gameobjectName, onConnectUnityCallback, onRecieveNetworkMessageCallback) {
    Connect(https, Pointer_stringify(ipadress), Pointer_stringify(username), Pointer_stringify(gameobjectName),
     Pointer_stringify(onConnectUnityCallback), Pointer_stringify(onRecieveNetworkMessageCallback));
  }

});