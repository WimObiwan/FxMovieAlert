// Write your Javascript code.
// Navigation Scripts to Show Header on Scroll-Up
jQuery(document).ready(function($) {
    var MQL = 1500;
    $('.sideBar').fadeOut('fast');

    //primary navigation slide-in effect
    if ($(window).width() >= MQL) {
        $(window).on('scroll', {
            }, 
            function() {
                var currentTop = $(window).scrollTop();
                if (currentTop >= 150) {
                    $('.sideBar').fadeIn('slow');
                } else {
                    $('.sideBar').fadeOut('slow');
                }
            });
    }
});

if ('serviceWorker' in navigator) {
    window.addEventListener('load', function() {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' })
            .then(function(registration) {
            // Registration was successful
            console.log('ServiceWorker registration successful with scope: ', registration.scope);
        }, function(err) {
            // registration failed :(
            console.log('ServiceWorker registration failed: ', err);
        });

        butInstall.addEventListener('click', async () => {
            console.log('👍', 'butInstall-clicked');
            const promptEvent = window.deferredPrompt;
            if (!promptEvent) {
                // The deferred prompt isn't available.
                return;
            }
            // Show the install prompt.
            promptEvent.prompt();
            // Log the result
            const result = await promptEvent.userChoice;
            console.log('👍', 'userChoice', result);
            // Reset the deferred prompt variable, since
            // prompt() can only be called once.
            window.deferredPrompt = null;
            // Hide the install button.
            installContainer.classList.toggle('hidden', true);
        });
    });

    window.addEventListener('beforeinstallprompt', (event) => {
        console.log('👍', 'beforeinstallprompt', event);
        // Stash the event so it can be triggered later.
        window.deferredPrompt = event;
        // Remove the 'hidden' class from the install button container
        installContainer.classList.toggle('hidden', false);
    });
}
