// 2020-01-29
var stunServer = "stun.l.google.com:19302";
var hdnStatus = document.getElementById('hdnStatus');
var lblStatus = document.getElementById('lblStatus');
var btnConnect = document.getElementById('btnConnect');
var remoteStream;
var offeredSdp;
var peerConn = null;
var started = false;
var connected = false;
var isRTCPeerConnection = true;

var mediaConstraints = {
    offerToReceiveAudio: 1,
    offerToReceiveVideo: 0
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
            subString: "Edge",
            identity: "Edge"
        },
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

//.mediaDevices.getUserMedia //|| navigator.getUserMedia || (navigator.getUserMedia = navigator.mozGetUserMedia ||
//navigator.webkitGetUserMedia || navigator.msGetUserMedia);
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

function IVLConnectApplicationID(applicationID) {
    var client = new HttpClient();
    client.get('https://ivlrest.voiceelements.com/webrtcip?appid=' + applicationID, function (response) {
        IVLConnect("wss://" + response + "/");
    });
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




var mediaConstraints = {
    offerToReceiveAudio: 1,
    offerToReceiveVideo: 0
};


async function init() {
    try {
        const stream = await navigator.mediaDevices.getUserMedia(constraints);
        successCallback(stream);

    } catch (e) {
        errorCallback(e);
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
        RaiseMessageEvent('IVLDisconnect() - Could not remove stream: ' + err.message);
    }

    ivlRemoteAudio.src = "";
    ivlSourceAudio.src = "";

    try {
        peerConn.close();
    }
    catch (err) {
        RaiseMessageEvent('IVLDisconnect() - Error closing peerconn');
    }
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
                else if (BrowserDetect.browser == "Firefox") {
                    dtmfSender = peerConn.getSenders().find(sender => sender.track == localAudioTrack);
                    logg('Created DTMF Sender');
                }
                else if (BrowserDetect.browser == "Edge") {
                    dtmfSender = peerConn.getSenders().find(sender => sender.track == localAudioTrack);
                    logg('Created DTMF Sender');
                }
                else if (BrowserDetect.browser == "Safari") {
                    dtmfSender = peerConn.getSenders().find(sender => sender.track == localAudioTrack);
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
                adjust.sdp = adjust.sdp.replace('a=mid:audio', getMid(offeredSdp));
                adjust.sdp = adjust.sdp.replace('a=group:BUNDLE audio', 'a=group:BUNDLE ' + getMid(offeredSdp).replace('a=mid:', ''));
                logg('new SDP: ' + adjust.sdp);
                peerConn.setRemoteDescription(new RTCSessionDescription(adjust));
            }
            // else if (BrowserDetect.browser == "Safari"){
            //     peerConn.setRemoteDescription(new RTCSessionDescription(adjust));
            // }
            else if (BrowserDetect.browser == "Edge") {
                adjust.sdp = adjust.sdp.replace('a=group:BUNDLE audio\r\n', '');
                adjust.sdp = adjust.sdp.replace('a=ice-lite\r\n', '');
                //adjust.sdp = adjust.sdp.replace('a=setup:active\r\n', '');
                //adjust.sdp = adjust.sdp.replace('c=IN IP4 0.0.0.0\r\n', 'c=IN IP4 209.105.253.154\r\n');
                adjust.sdp = adjust.sdp.replace('a=mid:audio', getMid(offeredSdp));
                //adjust.sdp = adjust.sdp.replace('a=rtcp:1 IN IP4 0.0.0.0\r\n', '');
                //adjust.sdp = adjust.sdp.replace('m=audio 1', 'm=audio 49154');
                //adjust.sdp = adjust.sdp.replace('a=rtcp-mux', 'a=ssrc:92976637 cname:7Z7u+TQ+aQq6XNF3\r\na=ssrc:92976637 mslabel:FW5hMwci4MqPSjkKdE--offJfjcHV5Nu76hT\r\na=ssrc:92976637 label:FW5hMwci4MqPSjkKdE--offJfjcHV5Nu76hT00\r\na=rtcp-mux');
                adjust.sdp = adjust.sdp + 'a=end-of-candidates\r\n'
                logg('new SDP: ' + adjust.sdp);

                try {
                    peerConn.setRemoteDescription(new RTCSessionDescription(adjust));
                }
                catch (e) {
                    logg('Exception: ' + e);

                }
            }
            else {
                //adjust.sdp = addMidsForFirefox(offeredSdp, adjust.sdp);
                adjust.sdp = adjust.sdp.replace('a=mid:audio', getMid(offeredSdp));
                adjust.sdp = adjust.sdp.replace('a=group:BUNDLE audio', 'a=group:BUNDLE ' + getMid(offeredSdp).replace('a=mid:', ''));
                logg('new SDP: ' + adjust.sdp);
                peerConn.setRemoteDescription(new RTCSessionDescription(adjust)).then(IVLOnRemoteStreamAdded, IVLOnRemoteStreamAddedError);
            }
            break;
        case "HmpElements.Server.IVLSocketCandidate":
            RaiseMessageEvent('IVLSocketOnMessage() - IVLSocketCandidate: ' + msg.candidate);
            if (BrowserDetect.browser == "Chrome") {
                var candidate = new RTCIceCandidate({ sdpMLineIndex: msg.label, candidate: msg.candidate });
                peerConn.addIceCandidate(candidate);
            }
            else if (BrowserDetect.browser == "Edge") {
                var candidate = new RTCIceCandidate({ sdpMLineIndex: msg.label, candidate: msg.candidate });
                peerConn.addIceCandidate(candidate);

            }
            else {
                var candidate = new RTCIceCandidate({ sdpMLineIndex: msg.label, candidate: msg.candidate });
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

function addMidsForFirefox(sdpOffer, sdpAnswer) {
    sdpOfferLines = sdpOffer.split("\r\n");

    var bundleLine = "";
    var audioMid = "";
    var videoMid = "";
    var nextMid = "";

    for (i = 0; i < sdpOfferLines.length; ++i) {
        if (sdpOfferLines[i].indexOf("a=group:BUNDLE") === 0) {
            bundleLine = sdpOfferLines[i];
        }
        else if (sdpOfferLines[i].indexOf("m=") === 0) {
            nextMid = sdpOfferLines[i].split(" ")[0];
        }
        else if (sdpOfferLines[i].indexOf("a=mid") === 0) {
            if (nextMid === "m=audio") {
                audioMid = sdpOfferLines[i];
            }
            else if (nextMid === "m=video") {
                videoMid = sdpOfferLines[i];
            }
        }
    }

    return sdpAnswer.replace(/a=group:BUNDLE.*/, bundleLine)
        .replace(/m=audio.*/, function (x) { return x.concat("\r\n").concat(audioMid) })
        .replace(/m=video.*/, function (x) { return x.concat("\r\n").concat(videoMid) });
}

function getMid(sdpOffer) {
    sdpOfferLines = sdpOffer.split("\r\n");

    for (i = 0; i < sdpOfferLines.length; ++i) {
        if (sdpOfferLines[i].indexOf("a=mid") === 0) {
            return sdpOfferLines[i];
        }
    }

}

function IVLOpenMedia() {
    logg('IVLOpenMedia()');
    if (webrtcDetectedBrowser == "Chrome") {
        try {
            navigator.webkitGetUserMedia({ audio: true, video: false }, successCallback, errorCallback);
        } catch (e) {
            navigator.webkitGetUserMedia("audio", successCallback, errorCallback);
        }
    }
    else if (webrtcDetectedBrowser == "Edge") {
        try {
            navigator.GetUserMedia();
        } catch (e) {
            navigator.mediaDevices.getUserMedia({ audio: true, video: false }).then(successCallback, errorCallback);
        }
    }
    else if (webrtcDetectedBrowser == "Firefox") {
        try {
            navigator.mozGetUserMedia();
        } catch (e) {
            navigator.mediaDevices.getUserMedia({ audio: true, video: false }).then(successCallback, errorCallback);
        }
    }
    else if (webrtcDetectedBrowser == "Safari") {
        init();

    }
    else
        alert('Incompatible browser');
}


const constraints = window.constraints = {
    audio: true,
    video: false
};


function successCallback(stream) {
    if (stream) {
        if (BrowserDetect.browser == "Chrome") {
            //ivlSourceAudio.src = window.webkitURL.createObjectURL(stream);
            ivlSourceAudio.srcObject = stream;
        }
        else if (BrowserDetect.browser == "Edge") {
            //ivlSourceAudio.src = window.webkitURL.createObjectURL(stream);
            ivlSourceAudio.srcObject = stream;
        }
        else if (BrowserDetect.browser == "Firefox") {
            //ivlSourceAudio.src = window.URL.createObjectURL(stream);
            ivlSourceAudio.srcObject = stream;
        }
        else if (BrowserDetect.browser == "Safari") {
            //ivlSourceAudio.src = window.URL.createObjectURL(stream);
            ivlSourceAudio.srcObject = stream;
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
        else if (BrowserDetect.browser == "Edge") {
            peerConn = new RTCPeerConnection();
        }
        else {
            peerConn = new RTCPeerConnection(pc_config, pc_constraints);
            peerConn.ontrack = function (event) {
                var streamRemote = window.URL.createObjectURL(event.streams[0]);
                ivlRemoteAudio.src = streamRemote;
            };
        }
        peerConn.onicecandidate = onIceCandidate;
    } catch (e) {
        //try {
        //    peerConn = new RTCPeerConnection('STUN ' + stunServer, onIceCandidate00);
        //    isRTCPeerConnection = false;
        //} catch (e) {
        logg("Failed to create PeerConnection, exception: " + e.message);
        //}
    }

    if (BrowserDetect.browser == "Firefox") {
        //peerConn.ontrack = IVLOnRemoteStreamAdded;
        peerConn.onremovestream = IVLOnRemoteStreamRemoved;
    }
    else {

        peerConn.onaddstream = IVLOnRemoteStreamAdded;
        peerConn.onremovestream = IVLOnRemoteStreamRemoved;
    }
}

function IVLOnRemoteStreamAdded(event) {
    logg("Added remote stream");
    if (BrowserDetect.browser == "Chrome") {
        //ivlRemoteAudio.src = window.webkitURL.createObjectURL(event.stream);
        ivlRemoteAudio.srcObject = event.stream;
        remoteStream = event.stream;
    } else if (BrowserDetect.browser === "Edge") {
        ivlRemoteAudio.srcObject = event.stream;
    } else {
        //ivlRemoteAudio.src = window.URL.createObjectURL(event.stream);
        ivlRemoteAudio.srcObject = event.stream;
        remoteStream = event.stream;
    }

    RaiseMediaStatusEvent(IVLMediaState.Connected);
}

function IVLOnRemoteStreamAddedError(event) {
    logg("IVLOnRemoteStreamAddedError Error");
    logg(event);
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
    offeredSdp = sessionDescription.sdp;
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
    var duration = 250;
    var gap = 50;

    if (dtmfSender) {
        //duration = document.getElementById("dtmf-tones-duration").value;
        //gap = document.getElementById("dtmf-tones-gap").value;

        if (BrowserDetect.browser == "Chrome") {
            dtmfSender.insertDTMF(tones, duration, gap);
        }
        else if (BrowserDetect.browser == "Safari") {
            dtmfSender.insertDTMF(tones, duration, gap);
        }
        else if (BrowserDetect.browser == "Edge") {
            dtmfSender.dtmf.insertDTMF(tones, duration, gap);
        }
        else {
            dtmfSender.dtmf.insertDTMF(tones, duration, gap);
        }

        logg("Sent Tone: " + tones);
    }
    else {
        logg("Error Tone: " + tones);
    }
}

var HttpClient = function () {
    this.get = function (aUrl, aCallback) {
        var anHttpRequest = new XMLHttpRequest();
        anHttpRequest.onreadystatechange = function () {
            if (anHttpRequest.readyState == 4 && anHttpRequest.status == 200)
                aCallback(anHttpRequest.responseText);
        }

        anHttpRequest.open("GET", aUrl, true);
        anHttpRequest.send(null);
    }
}

