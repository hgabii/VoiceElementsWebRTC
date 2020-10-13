// 2014-03-14
var stunServer = "stun.l.google.com:19302";
var hdnStatus = document.getElementById('hdnStatus');
var lblStatus = document.getElementById('lblStatus');
var btnConnect = document.getElementById('btnConnect');
var remoteStream;
var peerConn = null;
var started = false;
var connected = false;
var isRTCPeerConnection = true;
var mediaConstraints = {
    'mandatory': {
        'OfferToReceiveAudio': true,
        'OfferToReceiveVideo': false
    }
};

var activeCalls = new Array();
var pendingCalls = new Array();

var callid = "";

var dtmfSender = null;

var logg = function (s) { console.log(s); };

var IVLSocketState = Object.freeze({ Disconnected: 0, Permissions: 1, Connecting: 2, Connected: 3 });
var IVLMediaState = Object.freeze({ Disconnected: 0, Connected: 1 });

var ivlCurrentSocketState = IVLSocketState.Disconnected;
var ivlCurrentMediaState = IVLMediaState.Disconnected;
var ivlSocketUrl;
var ivlSocket;
var ivlMediaOpened = false;
var ivlLocalStream = null;
var ivlSourceAudio = document.getElementById('sourceAudio');
var ivlRemoteAudio = document.getElementById('remoteAudio');
var ivlLogs = false;

// BROWSER SPECIFIC VARIABLES
var webrtcDetectedBrowser = null;
var webRtcDetectedVersion = null;

//DETECT USER BROWSER
var BrowserDetect = {
    init: function () {
        this.browser = this.searchString(this.dataBrowser) || "An unknown browser";
        this.version = this.searchVersion(navigator.userAgent)
			|| this.searchVersion(navigator.appVersion)
			|| "an unknown version";
        this.OS = this.searchString(this.dataOS) || "an unknown OS";
    },
    searchString: function (data) {
        for (var i = 0; i < data.length; i++) {
            var dataString = data[i].string;
            var dataProp = data[i].prop;
            this.versionSearchString = data[i].versionSearch || data[i].identity;
            if (dataString) {
                if (dataString.indexOf(data[i].subString) != -1)
                    return data[i].identity;
            }
            else if (dataProp)
                return data[i].identity;
        }
    },
    searchVersion: function (dataString) {
        var index = dataString.indexOf(this.versionSearchString);
        if (index == -1) return;
        return parseFloat(dataString.substring(index + this.versionSearchString.length + 1));
    },
    dataBrowser: [
		{
		    string: navigator.userAgent,
		    subString: "Chrome",
		    identity: "Chrome"
		},
		{
		    string: navigator.userAgent,
		    subString: "OmniWeb",
		    versionSearch: "OmniWeb/",
		    identity: "OmniWeb"
		},
		{
		    string: navigator.vendor,
		    subString: "Apple",
		    identity: "Safari",
		    versionSearch: "Version"
		},
		{
		    prop: window.opera,
		    identity: "Opera",
		    versionSearch: "Version"
		},
		{
		    string: navigator.vendor,
		    subString: "iCab",
		    identity: "iCab"
		},
		{
		    string: navigator.vendor,
		    subString: "KDE",
		    identity: "Konqueror"
		},
		{
		    string: navigator.userAgent,
		    subString: "Firefox",
		    identity: "Firefox"
		},
		{
		    string: navigator.vendor,
		    subString: "Camino",
		    identity: "Camino"
		},
		{		// for newer Netscapes (6+)
		    string: navigator.userAgent,
		    subString: "Netscape",
		    identity: "Netscape"
		},
		{
		    string: navigator.userAgent,
		    subString: "MSIE",
		    identity: "Explorer",
		    versionSearch: "MSIE"
		},
		{
		    string: navigator.userAgent,
		    subString: "Gecko",
		    identity: "Mozilla",
		    versionSearch: "rv"
		},
		{ 		// for older Netscapes (4-)
		    string: navigator.userAgent,
		    subString: "Mozilla",
		    identity: "Netscape",
		    versionSearch: "Mozilla"
		}
    ],
    dataOS: [
		{
		    string: navigator.platform,
		    subString: "Win",
		    identity: "Windows"
		},
		{
		    string: navigator.platform,
		    subString: "Mac",
		    identity: "Mac"
		},
		{
		    string: navigator.userAgent,
		    subString: "iPhone",
		    identity: "iPhone/iPod"
		},
		{
		    string: navigator.platform,
		    subString: "Linux",
		    identity: "Linux"
		}
    ]

};
BrowserDetect.init();
webrtcDetectedBrowser = BrowserDetect.browser;

navigator.getUserMedia || (navigator.getUserMedia = navigator.mozGetUserMedia ||
navigator.webkitGetUserMedia || navigator.msGetUserMedia);
// END DETECT USER BROWSER

// NEW METHODS

function RaiseMessageEvent(msg) {

    logg(msg);

    if (ivlLogs == true && msg && window.CustomEvent) {
        var event = new CustomEvent("messageEvent", {
            detail: {
                message: msg,
                time: new Date(),
            },
            bubbles: true,
            cancelable: true
        });

        document.dispatchEvent(event);
    }
}

function RaiseSocketStatusEvent(newStatus) {

    var event = new CustomEvent("socketStatusEvent", {
        detail: {
            status: newStatus,
            time: new Date(),
        },
        bubbles: true,
        cancelable: true
    });

    document.dispatchEvent(event);
}

function RaiseMediaStatusEvent(newStatus) {

    var event = new CustomEvent("mediaStatusEvent", {
        detail: {
            status: newStatus,
            time: new Date(),
        },
        bubbles: true,
        cancelable: true
    });

    document.dispatchEvent(event);
}

function RaiseAuthenticationEvent(mod, exp) {

    var event = new CustomEvent("authenticationEvent", {
        detail: {
            modulus: mod,
            publicExponent: exp,
        },
        bubbles: true,
        cancelable: true
    });

    document.dispatchEvent(event);
}

function RaiseApplicationMessageEvent(msg) {

    var event = new CustomEvent("applicationMessageEvent", {
        detail: {
            message: msg,
        },
        bubbles: true,
        cancelable: true
    });

    document.dispatchEvent(event);
}

function IVLAuthenticate(usr, pwd) {
    IVLSocketSendMessage({ __type: 'HmpElements.Server.IVLSocketAuthenticationResponse', version: 1, username: usr, password: pwd });
}

function IVLConnect(wsUrl) {

    logg('IVLConnect() - ' + wsUrl);

    ivlSocketUrl = wsUrl;

    try {
        if (!ivlMediaOpened) {
            ivlCurrentSocketState = IVLSocketState.Permissions;
            RaiseSocketStatusEvent(IVLSocketState.Permissions);
            RaiseMessageEvent("Calling IVLOpenMedia...");
            IVLOpenMedia();
            return;
        }
        else {
            ivlCurrentSocketState = IVLSocketState.Permissions;
            IVLConnectInternal();
        }
    }
    catch (e) {
        RaiseMessageEvent('IVLConnect() Exception: ' + e.message);
    }
}

function IVLConnectInternal() {

    logg('IVLConnectInternal()');

    try {

        if (!ivlMediaOpened) {
            throw { message: 'Cannot Connect. No Permission.', func: 'IVLConnectInternal()' };
        }

        switch (ivlCurrentSocketState) {
            case IVLSocketState.Permissions:
                ivlSocketUrl = ivlSocketUrl;
                ivlCurrentSocketState = IVLSocketState.Connecting;
                RaiseSocketStatusEvent(IVLSocketState.Connecting);
                RaiseMessageEvent("Connecting...");
                IVLStartSocket();
                break;
            default:
                throw { message: 'Cannot Connect Now. Invalid State', func: 'IVLConnect()' };
        }
    }
    catch (e) {
        RaiseMessageEvent('IVLConnect() Exception: ' + e.message);
    }
}


function IVLDisconnect() {
    RaiseMessageEvent("Calling IVLDisconnect...");

    ivlSocket.close();
    ivlSocket = null;


    try {
        peerConn.removeStream(ivlLocalStream);
    }
    catch (err) {
        RaiseMessageEvent('IVLSocketOnMessage() - Could not remove stream: ' + err.message);
    }

    ivlRemoteAudio.src = "";
    ivlSourceAudio.src = "";

    peerConn.close();
    peerConn = null;

}

function IVLStartSocket() {
    if ('WebSocket' in window) {
        ivlSocket = new WebSocket(ivlSocketUrl);
        ivlSocket.addEventListener("error", IVLSocketOnError, false);
        ivlSocket.addEventListener("message", IVLSocketOnMessage, false);
        ivlSocket.addEventListener("open", IVLSocketOnOpen, false);
        ivlSocket.addEventListener("close", IVLSocketOnClose, false);
    }
    else {
        throw { message: 'WebSockets Are Not Supported', func: 'IVLStartSocket()' };
    }
}

function IVLSocketOnError(evt) {
    logg("ERROR: " + evt);
    ivlSocketCurrentState = IVLSocketState.Disconnected;
    RaiseSocketStatusEvent(IVLSocketState.Disconnected);
    RaiseMessageEvent('IVLSocketOnError()');
}

function IVLSocketOnOpen(evt) {
    RaiseMessageEvent('IVLSocketOnOpen() -- Opened Web Socket: ');
    IVLSocketSendMessage({ __type: 'HmpElements.Server.IVLSocketHandshake', version: 1, url: document.URL });
    RaiseSocketStatusEvent(IVLSocketState.Connected);
}

function IVLSocketOnClose(evt) {
    ivlCurrentSocketState = IVLSocketState.Disconnected;
    RaiseSocketStatusEvent(IVLSocketState.Disconnected);
    RaiseMessageEvent('IVLSocketOnClose()');
}

function IVLSocketOnMessage(evt) {
    logg("RECEIVED: " + evt.data);

    var msg = JSON.parse(evt.data);

    switch (msg.__type) {
        case "HmpElements.Server.IVLSocketAuthenticationChallenge":
            RaiseAuthenticationEvent(msg.modulus, msg.publicExponent);
            break;
        case "HmpElements.Server.IVLApplicationMessage":
            RaiseApplicationMessageEvent(msg.message);
            break;
        case "HmpElements.Server.IVLSocketResponse":
            RaiseMessageEvent('IVLSocketOnMessage() - Code: ' + msg.statusCode + ' Description: ' + msg.reasonPhrase);
            if (msg.statusCode > 299) {
                ivlSocket.close();
                ivlSocket = null;
                ivlRemoteAudio.src = "";
            }
            break;
        case "HmpElements.Server.IVLSocketModifyMedia":
            // TEST
            RaiseMessageEvent('IVLSocketOnMessage() - AudioRequest Type: ' + msg.mediaType + ' Option: ' + msg.mediaOption);
            if (msg.mediaOption == 'Start') {
                IVLStartPeerConnection();

                logg('Adding local stream...');
                peerConn.addStream(ivlLocalStream);
                var localAudioTrack = ivlLocalStream.getAudioTracks()[0];
                if (BrowserDetect.browser == "Chrome") {
                    dtmfSender = peerConn.createDTMFSender(localAudioTrack);
                    logg('Created DTMF Sender');
                }
                else {
                    logg('Could not create DTMF Sender');
                }

                started = true;
                logg("isRTCPeerConnection: " + isRTCPeerConnection);

                //create offer
                if (isRTCPeerConnection) {
                    peerConn.createOffer(setLocalAndSendMessage, offerfailed, mediaConstraints);
                } else {
                    var offer = peerConn.createOffer(mediaConstraints);
                    peerConn.setLocalDescription(peerConn.SDP_OFFER, offer);
                    IVLSocketSendMessage({
                        __type: 'HmpElements.Server.IVLSocketSdp',
                        version: 1,
                        sdp: offer.toSdp()
                    });
                    lblStatus.textContent = "Sent Offer.";
                    peerConn.startIce();
                }
            }
            else {
                RaiseMessageEvent('IVLSocketOnMessage() - Stopping the stream...');

                try {
                    peerConn.removeStream(ivlLocalStream);
                }
                catch (err) {
                    RaiseMessageEvent('IVLSocketOnMessage() - Could not remove stream: ' + err.message);
                }
                //peerConn.createOffer(setLocalAndSendMessage);

                // JMC
                ivlRemoteAudio.src = "";
                ivlSourceAudio.src = "";

                //if (peerConn) {
                //peerConn.removeStream(ivlLocalStream);
                //    ivlSourceAudio.src = "";
                peerConn.close();
                peerConn = null;
                //}

                RaiseMediaStatusEvent(IVLMediaState.Disconnected);
            }
            break;
        case "HmpElements.Server.IVLSocketSdp":
            RaiseMessageEvent('IVLSocketOnMessage() - IVLSocketSdp: ' + msg.sdp);

            var adjust = {
                type: 'answer',
                sdp: msg.sdp
            }
            if (BrowserDetect.browser == "Chrome") {
                peerConn.setRemoteDescription(new RTCSessionDescription(adjust));
            }
            else {
                peerConn.setRemoteDescription(new mozRTCSessionDescription(adjust));
            }
            break;
        case "HmpElements.Server.IVLSocketCandidate":
            RaiseMessageEvent('IVLSocketOnMessage() - IVLSocketCandidate: ' + msg.candidate);
            if (BrowserDetect.browser == "Chrome") {
                var candidate = new RTCIceCandidate({ sdpMLineIndex: msg.label, candidate: msg.candidate });
                peerConn.addIceCandidate(candidate);
            }
            else {
                var candidate = new mozRTCIceCandidate({ sdpMLineIndex: msg.label, candidate: msg.candidate });
                try {
                    peerConn.addIceCandidate(candidate);
                }
                catch (e) {
                    logg('Exception: ' + e);
                }
            }
            break;
        case "HmpElements.Server.IVLSocketBye":
            RaiseMessageEvent('IVLSocketOnMessage() - IVLSocketBye');
            RaiseSocketStatusEvent(IVLSocketState.Disconnected);
            IVLDisconnect();
            break;
        default:
            logg('Unexpected Message Received: ' + evt.data);
            break;
    }
}

function IVLSocketSendMessage(message) {
    var mymsg = JSON.stringify(message);
    RaiseMessageEvent('IVLSocketSendMessage() - ' + mymsg);
    ivlSocket.send(mymsg);
}

function IVLSocketSendApplicationMessage(message) {
    var mymsg = JSON.stringify(message);
    IVLSocketSendMessage({ __type: 'HmpElements.Server.IVLApplicationMessage', version: 1, message: mymsg });
}


function IVLOpenMedia() {
    logg('IVLOpenMedia()');
    //    if(webrtcDetectedBrowser == "Chrome")
    //    {
    //        try {
    //            navigator.webkitGetUserMedia({ audio: true, video: false }, successCallback, errorCallback);
    //        } catch (e) {
    //            navigator.webkitGetUserMedia("audio", successCallback, errorCallback);
    //        }
    //    }
    //    else if(webrtcDetectedBrowser == "Firefox")
    //    {
    //        try
    //        {
    //            navigator.mozGetUserMedia)
    //        }
    //    }

    try {
        navigator.getUserMedia({ audio: true, video: false }, successCallback, errorCallback);
    }
    catch (e) {
        navigator.getUserMedia("audio", successCallback, errorCallback);
    }

    function successCallback(stream) {
        if (stream) {
            if (BrowserDetect.browser == "Chrome") {
                ivlSourceAudio.src = window.webkitURL.createObjectURL(stream);
            }
            else if (BrowserDetect.browser == "Firefox") {
                ivlSourceAudio.src = window.URL.createObjectURL(stream);
            }
            else {
                alert('You are not using a WebRTC compatible browser');
            }
            ivlLocalStream = stream;
            RaiseMessageEvent('Successfully Opened Media.');
            ivlMediaOpened = true;
            IVLConnectInternal();
        }
        else {
            RaiseMessageEvent('Failed To Open Media.');
        }
    }
    function errorCallback(error) {
        logg('An error occurred: [CODE ' + error.code + ']');
        ivlCurrentSocketState = IVLSocketState.Disconnected;
        RaiseSocketStatusEvent(IVLSocketState.Disconnected);
        RaiseMessageEvent("Failed to Open Media!");
    }
}


function IVLStartPeerConnection() {
    try {
        logg("Starting Peer connection");
        var servers = [];
        servers.push({ 'url': 'stun:' + stunServer });
        var pc_config = { 'iceServers': servers };
        var pc_constraints = { "optional": [{ 'DtlsSrtpKeyAgreement': 'true' }] };
        if (BrowserDetect.browser == "Chrome") {
            peerConn = new webkitRTCPeerConnection(pc_config, pc_constraints);
        }
        else {
            peerConn = new mozRTCPeerConnection(pc_config, pc_constraints);
        }
        peerConn.onicecandidate = onIceCandidate;
    } catch (e) {
        try {
            peerConn = new RTCPeerConnection('STUN ' + stunServer, onIceCandidate00);
            isRTCPeerConnection = false;
        } catch (e) {
            logg("Failed to create PeerConnection, exception: " + e.message);
        }
    }

    peerConn.onaddstream = IVLOnRemoteStreamAdded;
    peerConn.onremovestream = IVLOnRemoteStreamRemoved;
}

function IVLOnRemoteStreamAdded(event) {
    logg("Added remote stream");
    if (BrowserDetect.browser == "Chrome") {
        ivlRemoteAudio.src = window.webkitURL.createObjectURL(event.stream);
    }
    else {
        ivlRemoteAudio.src = window.URL.createObjectURL(event.stream);
    }
    RaiseMediaStatusEvent(IVLMediaState.Connected);
}

function IVLOnRemoteStreamRemoved(event) {
    logg("Remove remote stream");
    ivlRemoteAudio.src = "";
    RaiseMediaStatusEvent(IVLMediaState.Disconnected);
}

function onIceCandidate(event) {
    if (event.candidate) {
        IVLSocketSendMessage({
            __type: 'HmpElements.Server.IVLSocketCandidate',
            version: 1,
            label: event.candidate.sdpMLineIndex,
            id: event.candidate.sdpMid,
            candidate: event.candidate.candidate
        });
    } else {
        IVLSocketSendMessage({
            __type: 'HmpElements.Server.IVLSocketCandidate',
            version: 1,
            status: 'EndOfCandidates'
        });
        logg("End of candidates.");
    }
}

function onIceCandidate00(candidate, moreToFollow) {
    if (candidate) {
        logg('Zero Method');
        IVLSocketSendMessage({
            __type: 'HmpElements.Server.IVLSocketCandidate',
            version: 1,
            label: candidate.label,
            id: 0,
            candidate: candidate.toSdp()
        });
    }
    if (!moreToFollow) {
        IVLSocketSendMessage({
            __type: 'HmpElements.Server.IVLSocketCandidate',
            version: 1,
            status: 'EndOfCandidates'
        });
        logg("End of candidates.");
    }
}

function setLocalAndSendMessage(sessionDescription) {
    peerConn.setLocalDescription(sessionDescription);

    IVLSocketSendMessage({
        __type: 'HmpElements.Server.IVLSocketSdp',
        version: 1,
        sdp: sessionDescription.sdp
    });
}

function setLocalAndSendMessage00(answer) {
    peerConn.setLocalDescription(peerConn.SDP_ANSWER, answer);
    IVLSocketSendMessage({ type: 'answer', sdp: answer.toSdp() });
    peerConn.startIce();
}

function offerfailed() {
    RaiseMessageEvent('WebRTC Connection Failure.');
}

//  //////////////////////////////////////////////////////////////////////////////////////////

function sendTone(tones) {
    if (dtmfSender) {
        //duration = document.getElementById("dtmf-tones-duration").value;
        //gap = document.getElementById("dtmf-tones-gap").value;
        var duration = 250;
        var gap = 50;
        dtmfSender.insertDTMF(tones, duration, gap);
        lblStatus.textContent = "Sent Tone: " + tones;
        //$('#lblStatus').text('Sent Tone: ' + tones);
    }
}
