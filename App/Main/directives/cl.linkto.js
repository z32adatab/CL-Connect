//Link To Directive

'use strict';

//Get directives module
var clConnectDirectives = angular.module("clConnectDirectives");
//Register directive
clConnectDirectives.directive("clLinkTo", ["$location", function ($location) {

    //Build directive
    return {

        //Directive is available to attributes
        restrict: "A",

        link: function (scope, $element, attrs) {

            /// <summary>
            /// Performs the necessary linking for the drop down directive.
            /// </summary>
            /// <param name="scope">The element's scope.</param>
            /// <param name="$element">The element being linked.</param>
            /// <param name="attrs">The element attributes.</param>

            $element.on("click", function () {
                $location.path(attrs.clLinkTo);
                scope.$apply();
            });

        }

    };

}]);