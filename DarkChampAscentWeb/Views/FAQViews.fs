module FAQView

open Falco.Markup
open UI
open DiscordBot

let private qa (q:XmlNode, a:XmlNode) =
    Elem.div [ ] [
        Elem.div [ Attr.class' "question" ] [ q ]

        Elem.div [ Attr.class' "answer" ] [ a ]
    ]

let supportedAsas =
    "Can I send Algo or any other ASAs except DarkCoin to bot wallet ?" |> Text.raw, 
    "No, only DarkCoin is supported. If you send Algo it will be considered as donation to cover txs fee." |> Text.raw

let howToDeposit =
    "How can I deposit tokens to my bot account?" |> Text.raw,
    Ui.howToDeposit

let howToWithdraw =
    "I deposited tokens to my balance but I want to withdraw now" |> Text.raw,
    Elem.div [ ] [
        Text.raw "Not supported. This is not exchange bot. Do not hold your coins here. Deposit -> Spend."
        Elem.br [ ]
        Text.raw "Remember the main rule in crypto: not your keys - not your coins."
    ]

let howCanIHelp =
    "I like what you're doing but I have neither NFT to play nor DarkCoins to spare" |> Text.raw,
    Elem.div [ ] [
        Text.raw "That's fine - it's community project. You may contribute in many other ways:"
        Elem.ul [] [
            Elem.li [] [ Text.raw "Send PR on github (starting from something small like improving English grammar or expanding QA section). Any contribution are most welcome!" ]
            Elem.li [] [ Text.raw "Provide feedback or give ideas how can we improve bot." ]
            Elem.li [] [ Text.raw "Just be active on discord - someone may even gift you NFT." ]
        ]
    ]

let coinsSafety =
    "Are my coins safe?" |> Text.raw,
    Elem.div [ ] [
        Text.raw "Not your keys - not your coins."
        Elem.br [ ]
        Text.raw "Technically, your bot account balance is basically nothing more than a record in the db."
        Elem.br [ ]
        Text.raw "Bot's wallet is a hot wallet, its private keys are stored on the server so it's much less secure than cold wallet. Keep it in mind when sending tokens."
    ]

let noAnswersToMyQ =
    "I don't see answer to my question or answer wasn't clear enough" |> Text.raw,
    $"Write to #{Channels.ChatChannel} channel on DarkCoin discord" |> Text.raw

let qss = [
    supportedAsas
    howToDeposit
    howToWithdraw
    howCanIHelp
    coinsSafety
    noAnswersToMyQ
]

let faqScript =
    Elem.script [] [
        Text.raw """
(function () {
  'use strict';

  // Initialize all .faq containers
  var faqContainers = document.querySelectorAll('.faq');
  if (!faqContainers.length) return;

  faqContainers.forEach(function (container, containerIndex) {
    var isAccordion = container.dataset.accordion === 'true';
    var inlineMode = container.classList.contains('inline');

    // collect only element children (your qa helper creates a wrapper div per Q/A)
    var items = Array.prototype.filter.call(container.children, function (c) { return c.nodeType === 1; });

    items.forEach(function (item, i) {
      var question = item.querySelector('.question');
      var answer = item.querySelector('.answer');
      if (!question || !answer) return;

      // Ensure unique id for answer for aria-controls
      if (!answer.id) answer.id = 'faq-' + containerIndex + '-' + i + '-answer';

      // Make question accessible as a button if not already
      question.setAttribute('role', 'button');
      question.setAttribute('aria-controls', answer.id);
      question.tabIndex = question.hasAttribute('tabindex') ? question.getAttribute('tabindex') : 0;

      // Track expanded state via aria-expanded and item.open class
      var expanded = item.classList.contains('open');
      question.setAttribute('aria-expanded', expanded ? 'true' : 'false');
      answer.setAttribute('aria-hidden', expanded ? 'false' : 'true');

      // Initialize answer height for smooth animation
      if (expanded) {
        // ensure it's visible and allow natural height after transition
        answer.style.height = answer.scrollHeight + 'px';
        // after next frame, clear height to let it become auto
        requestAnimationFrame(function () {
          answer.addEventListener('transitionend', function clearHeight(e) {
            if (e.propertyName === 'height') {
              answer.style.height = 'auto';
              answer.removeEventListener('transitionend', clearHeight);
            }
          });
        });
      } else {
        answer.style.height = '0px';
      }

      // helper to recalc open height (useful when content changes)
      function refreshOpenHeight(a) {
        if (!a) return;
        if (a.style.height === 'auto') return;
        // set to the current scrollHeight so CSS animation can run
        a.style.height = a.scrollHeight + 'px';
      }

      // Toggle helpers (use height animation)
      function closeElement(el, q, a) {
        el.classList.remove('open');
        if (q) q.setAttribute('aria-expanded', 'false');
        if (a) {
          a.setAttribute('aria-hidden', 'true');
          // from auto or px -> px (current) then to 0 to animate
          var current = a.scrollHeight;
          a.style.height = current + 'px';
          // force reflow
          a.offsetHeight;
          requestAnimationFrame(function () {
            a.style.height = '0px';
          });
        }
      }

      function openElement(el, q, a) {
        if (inlineMode) return;
        // If accordion behavior requested, close siblings
        if (isAccordion) {
          items.forEach(function (other) {
            if (other !== el && other.classList.contains('open')) {
              var oq = other.querySelector('.question');
              var oa = other.querySelector('.answer');
              closeElement(other, oq, oa);
            }
          });
        }

        el.classList.add('open');
        if (q) q.setAttribute('aria-expanded', 'true');
        if (a) {
          a.setAttribute('aria-hidden', 'false');

          // Ensure we measure from 0 -> full height to animate cleanly
          a.style.height = '0px';
          // force reflow
          a.offsetHeight;
          // set to measured height to trigger transition
          requestAnimationFrame(function () {
            a.style.height = a.scrollHeight + 'px';
          });

          // After expansion, switch to auto so content changes don't get clipped
          a.addEventListener('transitionend', function clearHeight(e) {
            if (e.propertyName === 'height') {
              // If still open, allow natural height
              if (el.classList.contains('open')) {
                a.style.height = 'auto';
              }
              a.removeEventListener('transitionend', clearHeight);
            }
          });
        }
      }

      function toggleElement() {
        if (item.classList.contains('open')) {
          closeElement(item, question, answer);
        } else {
          openElement(item, question, answer);
        }
      }

      // Click toggles
      question.addEventListener('click', function (ev) {
        ev.preventDefault();
        toggleElement();
      });

      // Keyboard controls: Enter/Space toggle, Up/Down navigate
      question.addEventListener('keydown', function (ev) {
        var key = ev.key || ev.keyCode;
        if (key === 'Enter' || key === ' ' || key === 13 || key === 32) {
          ev.preventDefault();
          toggleElement();
          return;
        }
        if (key === 'ArrowDown' || key === 'Down' || key === 40) {
          ev.preventDefault();
          // focus next question
          var next = items[(i + 1) % items.length];
          if (next) {
            var nq = next.querySelector('.question');
            if (nq) nq.focus();
          }
          return;
        }
        if (key === 'ArrowUp' || key === 'Up' || key === 38) {
          ev.preventDefault();
          var prev = items[(i - 1 + items.length) % items.length];
          if (prev) {
            var pq = prev.querySelector('.question');
            if (pq) pq.focus();
          }
          return;
        }
      });

      // When content inside answer changes dynamically (images, etc.) ensure opened panels adapt
      var ro = new MutationObserver(function () {
        if (item.classList.contains('open')) {
          // If height is 'auto' we don't need to do anything; otherwise refresh the measured height
          if (answer.style.height !== 'auto') {
            answer.style.height = answer.scrollHeight + 'px';
            // clear after transition
            answer.addEventListener('transitionend', function clearHeight(e) {
              if (e.propertyName === 'height') {
                if (item.classList.contains('open')) answer.style.height = 'auto';
                answer.removeEventListener('transitionend', clearHeight);
              }
            });
          }
        }
      });
      ro.observe(answer, { childList: true, subtree: true, characterData: true });

      // Also watch for images that might load after initial measurement
      var imgs = answer.querySelectorAll('img');
      imgs.forEach(function (img) {
        if (!img.complete) {
          img.addEventListener('load', function () {
            // if open, update height; if closed nothing to do
            if (item.classList.contains('open')) {
              // if height is auto, leave it; else recalc
              if (answer.style.height !== 'auto') {
                answer.style.height = answer.scrollHeight + 'px';
              }
            }
          }, { once: true });
        }
      });

      // Optional: expose a refresh method on the item for external code
      item.refreshFaqHeight = function () {
        if (item.classList.contains('open')) {
          if (answer.style.height !== 'auto') {
            answer.style.height = answer.scrollHeight + 'px';
            requestAnimationFrame(function () {
              answer.style.height = 'auto';
            });
          }
        }
      };
    });
  });
})();
"""
    ]

let faqView =
    Elem.main [
        Attr.class' "faq"
        Attr.role "main"
    ] [
        yield! qss |> List.map qa
        yield faqScript
    ]