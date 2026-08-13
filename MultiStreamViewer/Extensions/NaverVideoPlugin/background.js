var port = {};

chrome.runtime.onMessage.addListener(function (message, sender, sendResponse) {

    if (message.type === "contentScriptLoaded") {

        console.log(
            "[NaverVideoPlugin] contentScriptLoaded:",
            sender.tab ? sender.tab.url : "no tab"
        );

        sendResponse({
            acknowledged: true,
            nativePorts: Object.keys(port)
        });

        return true;
    }

    if (message.type === "debugNative") {

        sendResponse({
            nativePorts: Object.keys(port)
        });

        return true;
    }

    return true;
});


chrome.runtime.onMessageExternal.addListener(
    function (request, sender, sendResponse) {

        console.log(
            "[NLiveConnector] EXTERNAL MESSAGE",
            request,
            sender
        );

        // CHZZK -> 확장 프로그램 외부 메시지가
        // 실제로 background까지 들어왔는지 페이지 Console에서도 확인
        sendDebugToTab(
            sender.tab ? sender.tab.id : null,
            "[NLiveConnector] EXTERNAL MESSAGE: " +
            JSON.stringify(request)
        );


        if (request.svc == "install") {

            sendResponse("install");

            return;
        }


        if (
            request.svc == undefined ||
            request.ver == undefined ||
            request.param == undefined
        ) {

            sendResponse("param");

            return;
        }


        var param = request.param;

        var svc = request.svc;

        var ver = request.ver;

        param.tabId = sender.tab.id;

        param.svc = svc;

        param.ver = ver;


        var hostName = svc + "_" + ver;


        // 어떤 Native Messaging Host를 찾는지 확인
        sendDebugToTab(
            param.tabId,
            "[NLiveConnector] hostName: " + hostName
        );


        if (port[svc] == undefined) {

            console.log(
                "[NLiveConnector] sendNativeMessage:",
                hostName
            );


            sendDebugToTab(
                param.tabId,
                "[NLiveConnector] sendNativeMessage: " +
                hostName
            );


            chrome.runtime.sendNativeMessage(
                hostName,
                {},
                function (response) {

                    console.log(
                        "[NLiveConnector] sendNativeMessage response:",
                        response,
                        "lastError:",
                        chrome.runtime.lastError
                    );


                    sendDebugToTab(
                        param.tabId,
                        "[NLiveConnector] sendNativeMessage response: " +
                        JSON.stringify(response) +
                        " / lastError: " +
                        (
                            chrome.runtime.lastError
                                ? chrome.runtime.lastError.message
                                : "none"
                        )
                    );


                    if (response == undefined) {

                        // proxy app not installed
                        var error = {};

                        error.err = true;

                        error.errCode = 1000;

                        error.tabId = param.tabId;

                        sendWebMessage(error);

                    } else {

                        // proxy app installed
                        executeProxy(param);

                        sendNativeMessage(param);
                    }
                }
            );

        } else {

            sendNativeMessage(param);
        }
    }
);


function sendDebugToTab(tabId, message) {

    if (tabId === undefined || tabId === null) {
        return;
    }


    chrome.tabs.sendMessage(
        parseInt(tabId),
        {
            type: "nliveDebug",
            message: message
        },
        function () {

            if (chrome.runtime.lastError) {

                console.warn(
                    "[NLiveConnector] debug send failed:",
                    chrome.runtime.lastError.message
                );
            }
        }
    );
}


function onNativeMessage(msg) {

    if (msg == undefined || msg == null) {
        return;
    }


    console.log(
        "[NLiveConnector] native response:",
        msg
    );


    sendDebugToTab(
        msg.tabId,
        "[NLiveConnector] native response: " +
        JSON.stringify(msg)
    );


    sendWebMessage(msg);
}


function sendWebMessage(msg) {

    if (msg.tabId === undefined || msg.tabId === null) {

        console.warn(
            "[ext] not found tabId in message."
        );

        return;
    }


    const tabId = msg.tabId;

    delete msg.tabId;


    chrome.tabs.get(
        parseInt(tabId),
        function (tab) {

            if (chrome.runtime.lastError) {

                console.error(
                    "[ext] failed tab query:",
                    chrome.runtime.lastError.message
                );

                return;
            }


            const data = JSON.stringify(msg);


            chrome.tabs.sendMessage(
                parseInt(tabId),
                data,
                function (response) {

                    if (chrome.runtime.lastError) {
                        return;
                    }
                }
            );
        }
    );
}


function onDisconnect(msg) {

    console.error(
        "[NLiveConnector] DISCONNECTED:",
        msg,
        chrome.runtime.lastError
    );


    if (msg.name != undefined && msg.name != null) {

        delete port[msg.name];
    }
}


function sendNativeMessage(param) {

    var data = {
        'param': param
    };


    console.log(
        "[NLiveConnector] sendNativeMessage:",
        param
    );


    sendDebugToTab(
        param.tabId,
        "[NLiveConnector] sendNativeMessage: " +
        JSON.stringify(param)
    );


    port[param.svc].postMessage(data);
}


function executeProxy(param) {

    var svc = param.svc;


    if (port[svc] == undefined) {

        var hostName = svc + "_" + param.ver;


        console.log(
            "[NLiveConnector] connectNative:",
            hostName
        );


        sendDebugToTab(
            param.tabId,
            "[NLiveConnector] connectNative: " +
            hostName
        );


        port[svc] =
            chrome.runtime.connectNative(hostName);


        port[svc].onMessage.addListener(
            onNativeMessage
        );


        port[svc].onDisconnect.addListener(
            onDisconnect
        );


        port[svc].name = svc;
    }
}


chrome.tabs.onRemoved.addListener(
    function callback(tabId, removeInfo) {

        for (let svc in port) {

            if (
                typeof port[svc].disconnect === 'function'
            ) {

                port[svc].disconnect();

                delete port[svc];
            }
        }
    }
);