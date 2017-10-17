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
