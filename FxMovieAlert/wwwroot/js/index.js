// Update the count down every 1 minute
function eachMinute() {
    // Get todays date and time
    var now = new Date().getTime();

    var timingElements = document.getElementsByClassName("timing");
    [].forEach.call(timingElements, function (timingElement) {
        var startTime = new Date(timingElement.getAttribute("data-timing"));

        var minutes = Math.ceil((startTime - now) / 60000);

        if (minutes > 60) {
            timingElement.innerHTML = "";
            timingElement.classList.remove("alert-warning");
            timingElement.classList.remove("alert-danger");
            timingElement.style.display = "none";
        } else if (minutes >= 2) {
            timingElement.classList.add("alert-warning");
            timingElement.classList.remove("alert-danger");
            timingElement.innerHTML = `Deze film begint over ${minutes} minuten.`;
            timingElement.style.display = null;
        } else if (minutes >= -1) {
            timingElement.classList.add("alert-warning");
            timingElement.classList.remove("alert-danger");
            timingElement.innerHTML = `Deze film begint nu.`;
            timingElement.style.display = null;
        } else {
            timingElement.classList.remove("alert-warning");
            timingElement.classList.add("alert-danger");
            timingElement.innerHTML = `Deze film is al ${-minutes} minuten bezig.`;
            timingElement.style.display = null;

            if (minutes < -30)
            {
                var current = timingElement.closest("tr");
                var previous = current.previousElementSibling;
                var next = current.nextElementSibling;
                if (next != null && next.classList.contains("ad"))
                {
                    next = next.nextSibling;
                }
                current.remove();
                if (previous != null && next != null 
                    && previous.classList.contains("date-header")
                    && next.classList.contains("date-header"))
                {
                    previous.remove();
                }
            }
        }
    });
}

var time = new Date(),
    secondsRemaining = (60 - time.getSeconds()) * 1000;


setTimeout(function() {
    setInterval(eachMinute, 60000);
    eachMinute();
}, secondsRemaining);

window.addEventListener('load', function (event) {
    eachMinute();
});
