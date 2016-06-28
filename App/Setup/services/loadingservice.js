(function () {
    'use strict';

    angular
        .module('clConnectServices')
        .factory('loadingservice', loadingservice);

    loadingservice.$inject = ['$rootScope'];

    function loadingservice($rootScope) {
        /// <summary>
        /// Service which handles the loading spinner.
        /// </summary>

        // The css of the center block
        var cssCenterBlock = {
            border: 'none',
            padding: '15px',
            backgroundColor: '#000',
            '-webkit-border-radius': '10px',
            '-moz-border-radius': '10px',
            'border-radius': '10px',
            opacity: 0.7,
            color: '#fff',
            left: 'calc(50% - 125px)',
            width: '250px'
        };

        // The css of the overlay
        var cssOverlay = {
            backgroundColor: '#000',
            opacity: 0.0,
            cursor: 'wait'
        };

        // The message to display in html
        var message = '<div style="height:150px;"><div id="spinner" style="position: absolute;display: block;top: 60%;left: 50%;"></div><h2 style="text-align:center;">Loading</h2></div>';

        // fade times
        var fadeIn = 100;
        var fadeOut = 400;
        var opts = {
            lines: 13, // The number of lines to draw
            length: 0, // The length of each line
            width: 10, // The line thickness
            radius: 30, // The radius of the inner circle
            corners: 1, // Corner roundness (0..1)
            rotate: 0, // The rotation offset
            direction: 1, // 1: clockwise, -1: counterclockwise
            color: '#fff', // #rgb or #rrggbb or array of colors
            speed: 1, // Rounds per second
            trail: 75, // Afterglow percentage
            shadow: false, // Whether to render a shadow
            hwaccel: false, // Whether to use hardware acceleration
            className: 'spinner', // The CSS class to assign to the spinner
            zIndex: 2000, // The z-index (defaults to 2000000000)
            top: 'auto', // Top position relative to parent in px
            left: 'auto' // Left position relative to parent in px
        };
        var spinner = new Spinner(opts).spin(/*target*/);

        var service = {
            run: function () {
                /// <summary>
                /// Sets up the service.
                /// </summary>
                $rootScope.$watch("requests", function (newValue) {
                    if (newValue > 0) {
                        $.blockUI({

                            css: cssCenterBlock,
                            overlayCSS: cssOverlay,
                            message: message,
                            fadeIn: fadeIn,
                            fadeOut: fadeOut,
                            baseZ: 2000 // Set to always on top 
                        });
                        $("#spinner").append(spinner.el);
                    } else {
                        $.unblockUI();
                        $rootScope.$broadcast("clconnectFinishedLoading");
                    }
                });
            }
        };

        return service;
    }
})();