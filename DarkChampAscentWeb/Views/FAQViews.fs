module FAQView

open Falco.Markup
open UI
open DiscordBot

let private qa (q:XmlNode, a:XmlNode) =
    Elem.div [ ] [
        Elem.div [ Attr.class' "question"; Attr.tabindex "0"; Attr.role "button" ] [ q ]
        Elem.div [ Attr.class' "answer" ] [
            Elem.div [ ] [ a ]
        ]
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

  var faqContainers = document.querySelectorAll('.faq');
  if (!faqContainers.length) return;

  faqContainers.forEach(function (container, containerIndex) {
    var isAccordion = container.dataset.accordion === 'true';
    var items = Array.prototype.filter.call(container.children, function (c) { return c.nodeType === 1; });

    items.forEach(function (item, i) {
      var question = item.querySelector('.question');
      var answer = item.querySelector('.answer');
      if (!question || !answer) return;

      // Set aria attributes
      if (!answer.id) answer.id = 'faq-' + containerIndex + '-' + i + '-answer';
      question.setAttribute('aria-controls', answer.id);
      question.setAttribute('aria-expanded', item.classList.contains('open') ? 'true' : 'false');

      // Toggle on click
      question.addEventListener('click', function (ev) {
        ev.preventDefault();
        var isOpen = item.classList.contains('open');

        // Accordion: close others
        if (isAccordion && !isOpen) {
          items.forEach(function (other) {
            if (other !== item && other.classList.contains('open')) {
              other.classList.remove('open');
              var oq = other.querySelector('.question');
              if (oq) oq.setAttribute('aria-expanded', 'false');
            }
          });
        }

        item.classList.toggle('open');
        question.setAttribute('aria-expanded', item.classList.contains('open') ? 'true' : 'false');
      });

      // Keyboard: Enter/Space toggle, Arrow keys navigate
      question.addEventListener('keydown', function (ev) {
        if (ev.key === 'Enter' || ev.key === ' ') {
          ev.preventDefault();
          question.click();
        } else if (ev.key === 'ArrowDown') {
          ev.preventDefault();
          var next = items[(i + 1) % items.length];
          if (next) next.querySelector('.question').focus();
        } else if (ev.key === 'ArrowUp') {
          ev.preventDefault();
          var prev = items[(i - 1 + items.length) % items.length];
          if (prev) prev.querySelector('.question').focus();
        }
      });
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